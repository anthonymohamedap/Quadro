using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IBestelbonExporter
{
    ExportFormaat Formaat { get; }
    Task<ExportResult> ExportAsync(Bestelbon bestelbon, string exportFolder);
}
