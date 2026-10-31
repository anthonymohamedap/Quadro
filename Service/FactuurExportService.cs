using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class FactuurExportService : IFactuurExportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IReadOnlyDictionary<ExportFormaat, IFactuurExporter> _exporters;
    private readonly ILogger<FactuurExportService>? _logger;

    public FactuurExportService(
        IDbContextFactory<AppDbContext> factory,
        IEnumerable<IFactuurExporter> exporters,
        ILogger<FactuurExportService>? logger = null)
    {
        _factory = factory;
        _exporters = exporters.ToDictionary(x => x.Formaat, x => x);
        _logger = logger;
    }

    public async Task<ExportResult> ExportAsync(int factuurId, ExportFormaat formaat, string exportFolder)
    {
        _logger?.LogInformation("Factuur export gestart. FactuurId={FactuurId}, Formaat={Formaat}, Folder={Folder}", factuurId, formaat, exportFolder);

        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
        if (factuur is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (factuur.Status != FactuurStatus.KlaarVoorExport)
            throw new InvalidOperationException("Export mag enkel vanuit status KlaarVoorExport.");

        if (!_exporters.TryGetValue(formaat, out var exporter))
            throw new InvalidOperationException($"Geen exporter geregistreerd voor formaat {formaat}.");

        await LaadBetalingenAsync(db, factuur);
        var result = await exporter.ExportAsync(factuur, exportFolder);
        if (!result.Success)
        {
            _logger?.LogWarning("Factuur export mislukt. FactuurId={FactuurId}, Message={Message}", factuurId, result.Message);
            return result;
        }

        factuur.Status = FactuurStatus.Geexporteerd;
        factuur.ExportPad = result.BestandPad;
        factuur.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger?.LogInformation("Factuur export gelukt. FactuurId={FactuurId}, Bestand={Pad}", factuurId, result.BestandPad);

        return result;
    }

    public async Task<ExportResult> GeneratePreviewAsync(int factuurId, ExportFormaat formaat, string exportFolder)
    {
        _logger?.LogInformation("Factuur preview gestart. FactuurId={FactuurId}, Formaat={Formaat}, Folder={Folder}", factuurId, formaat, exportFolder);

        await using var db = await _factory.CreateDbContextAsync();
        var factuur = await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
        if (factuur is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (!_exporters.TryGetValue(formaat, out var exporter))
            throw new InvalidOperationException($"Geen exporter geregistreerd voor formaat {formaat}.");

        await LaadBetalingenAsync(db, factuur);
        return await exporter.ExportAsync(factuur, exportFolder);
    }

    /// <summary>US-82 — vaste, terug te vinden map voor afgedrukte bestelbonnen.</summary>
    public static string BestelbonnenMap => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QuadroApp", "Bestelbonnen");

    public async Task<ExportResult> AfdrukkenAsync(int factuurId, string? exportFolder = null)
    {
        var folder = exportFolder ?? BestelbonnenMap;
        _logger?.LogInformation("Bestelbon afdrukken. FactuurId={FactuurId}, Folder={Folder}", factuurId, folder);
        await using var db = await _factory.CreateDbContextAsync();

        var factuur = await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
        if (factuur is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (!_exporters.TryGetValue(ExportFormaat.Pdf, out var exporter))
            throw new InvalidOperationException("Geen PDF-exporter geregistreerd.");

        await LaadBetalingenAsync(db, factuur);

        ExportResult result;
        try
        {
            result = await exporter.ExportAsync(factuur, folder);
        }
        catch (IOException ex)
        {
            // Typisch op Windows: de vorige PDF van deze bon staat nog open in een PDF-lezer.
            _logger?.LogWarning(ex, "Bestelbon afdrukken mislukt (bestand in gebruik). FactuurId={FactuurId}", factuurId);
            return ExportResult.Fail("De PDF van deze bestelbon is nog geopend. Sluit hem en probeer opnieuw.");
        }

        if (!result.Success)
            return result;

        // Een voorlopige bon (nog niet bevestigd) gaat niet naar de boekhouding: status blijft.
        if (!factuur.IsVoorlopig && factuur.Status is FactuurStatus.Draft or FactuurStatus.KlaarVoorExport)
            factuur.Status = FactuurStatus.Geexporteerd;
        factuur.ExportPad = result.BestandPad;
        factuur.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return result;
    }

    /// <summary>US-73 — betalingen meegeven aan de PDF (oudste eerst).</summary>
    private static async Task LaadBetalingenAsync(AppDbContext db, Factuur factuur)
    {
        factuur.GeregistreerdeBetalingen = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.FactuurId == factuur.Id && o.Soort == OntvangstSoort.Betaling)
            .OrderBy(o => o.Datum).ThenBy(o => o.Id)
            .ToListAsync();
    }
}
