using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IBestelbonExportService
{
    Task<ExportResult> GeneratePreviewAsync(int bestelbonId, ExportFormaat formaat, string exportFolder);
    Task<ExportResult> ExportAsync(int bestelbonId, ExportFormaat formaat, string exportFolder);

    /// <summary>
    /// US-82 — bestelbon afdrukken in elke status: PDF in de vaste map
    /// (standaard Documenten/QuadroApp/Bestelbonnen). Een Draft- of KlaarVoorExport-bon gaat
    /// intern naar Geexporteerd (exportpad bewaard); Betaald en Geannuleerd blijven zoals ze zijn.
    /// </summary>
    Task<ExportResult> AfdrukkenAsync(int bestelbonId, string? exportFolder = null);
}
