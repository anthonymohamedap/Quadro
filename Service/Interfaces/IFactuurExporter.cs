using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IFactuurExporter
{
    ExportFormaat Formaat { get; }
    Task<ExportResult> ExportAsync(Factuur factuur, string exportFolder);
}
