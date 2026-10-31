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

public sealed class BestelbonExportService : IBestelbonExportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IReadOnlyDictionary<ExportFormaat, IBestelbonExporter> _exporters;
    private readonly ILogger<BestelbonExportService>? _logger;

    public BestelbonExportService(
        IDbContextFactory<AppDbContext> factory,
        IEnumerable<IBestelbonExporter> exporters,
        ILogger<BestelbonExportService>? logger = null)
    {
        _factory = factory;
        _exporters = exporters.ToDictionary(x => x.Formaat, x => x);
        _logger = logger;
    }

    public async Task<ExportResult> ExportAsync(int bestelbonId, ExportFormaat formaat, string exportFolder)
    {
        _logger?.LogInformation("Bestelbon export gestart. BestelbonId={BestelbonId}, Formaat={Formaat}, Folder={Folder}", bestelbonId, formaat, exportFolder);

        await using var db = await _factory.CreateDbContextAsync();
        var bestelbon = await db.Bestelbonnen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == bestelbonId);
        if (bestelbon is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (bestelbon.Status != BestelbonStatus.KlaarVoorExport)
            throw new InvalidOperationException("Export mag enkel vanuit status KlaarVoorExport.");

        if (!_exporters.TryGetValue(formaat, out var exporter))
            throw new InvalidOperationException($"Geen exporter geregistreerd voor formaat {formaat}.");

        await LaadBetalingenAsync(db, bestelbon);
        var result = await exporter.ExportAsync(bestelbon, exportFolder);
        if (!result.Success)
        {
            _logger?.LogWarning("Bestelbon export mislukt. BestelbonId={BestelbonId}, Message={Message}", bestelbonId, result.Message);
            return result;
        }

        bestelbon.Status = BestelbonStatus.Geexporteerd;
        bestelbon.ExportPad = result.BestandPad;
        bestelbon.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();

        _logger?.LogInformation("Bestelbon export gelukt. BestelbonId={BestelbonId}, Bestand={Pad}", bestelbonId, result.BestandPad);

        return result;
    }

    public async Task<ExportResult> GeneratePreviewAsync(int bestelbonId, ExportFormaat formaat, string exportFolder)
    {
        _logger?.LogInformation("Bestelbon preview gestart. BestelbonId={BestelbonId}, Formaat={Formaat}, Folder={Folder}", bestelbonId, formaat, exportFolder);

        await using var db = await _factory.CreateDbContextAsync();
        var bestelbon = await db.Bestelbonnen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == bestelbonId);
        if (bestelbon is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (!_exporters.TryGetValue(formaat, out var exporter))
            throw new InvalidOperationException($"Geen exporter geregistreerd voor formaat {formaat}.");

        await LaadBetalingenAsync(db, bestelbon);
        return await exporter.ExportAsync(bestelbon, exportFolder);
    }

    /// <summary>US-82 — vaste, terug te vinden map voor afgedrukte bestelbonnen.</summary>
    public static string BestelbonnenMap => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QuadroApp", "Bestelbonnen");

    public async Task<ExportResult> AfdrukkenAsync(int bestelbonId, string? exportFolder = null)
    {
        var folder = exportFolder ?? BestelbonnenMap;
        _logger?.LogInformation("Bestelbon afdrukken. BestelbonId={BestelbonId}, Folder={Folder}", bestelbonId, folder);
        await using var db = await _factory.CreateDbContextAsync();

        var bestelbon = await db.Bestelbonnen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == bestelbonId);
        if (bestelbon is null)
            throw new InvalidOperationException("Bestelbon niet gevonden.");

        if (!_exporters.TryGetValue(ExportFormaat.Pdf, out var exporter))
            throw new InvalidOperationException("Geen PDF-exporter geregistreerd.");

        await LaadBetalingenAsync(db, bestelbon);

        ExportResult result;
        try
        {
            result = await exporter.ExportAsync(bestelbon, folder);
        }
        catch (IOException ex)
        {
            // Typisch op Windows: de vorige PDF van deze bon staat nog open in een PDF-lezer.
            _logger?.LogWarning(ex, "Bestelbon afdrukken mislukt (bestand in gebruik). BestelbonId={BestelbonId}", bestelbonId);
            return ExportResult.Fail("De PDF van deze bestelbon is nog geopend. Sluit hem en probeer opnieuw.");
        }

        if (!result.Success)
            return result;

        // Een voorlopige bon (nog niet bevestigd) gaat niet naar de boekhouding: status blijft.
        if (!bestelbon.IsVoorlopig && bestelbon.Status is BestelbonStatus.Draft or BestelbonStatus.KlaarVoorExport)
            bestelbon.Status = BestelbonStatus.Geexporteerd;
        bestelbon.ExportPad = result.BestandPad;
        bestelbon.BijgewerktOp = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return result;
    }

    /// <summary>US-73 — betalingen meegeven aan de PDF (oudste eerst).</summary>
    private static async Task LaadBetalingenAsync(AppDbContext db, Bestelbon bestelbon)
    {
        bestelbon.GeregistreerdeBetalingen = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.BestelbonId == bestelbon.Id && o.Soort == OntvangstSoort.Betaling)
            .OrderBy(o => o.Datum).ThenBy(o => o.Id)
            .ToListAsync();
    }
}
