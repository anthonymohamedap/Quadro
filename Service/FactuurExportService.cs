using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.Generic;
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
        await FactuurSchemaUpgrade.EnsureAsync(db);
        var factuur = await db.Facturen.Include(x => x.Lijnen.OrderBy(l => l.Sortering)).FirstOrDefaultAsync(x => x.Id == factuurId);
        if (factuur is null)
            throw new InvalidOperationException("Factuur niet gevonden.");

        if (factuur.Status != FactuurStatus.KlaarVoorExport)
            throw new InvalidOperationException("Export mag enkel vanuit status KlaarVoorExport.");

        if (!_exporters.TryGetValue(formaat, out var exporter))
            throw new InvalidOperationException($"Geen exporter geregistreerd voor formaat {formaat}.");

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
}
