using QuadroApp.Model.Import;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public interface IImportService
{
    Task<ImportResult<T>> DryRunAsync<T>(Stream stream, IExcelMap<T> map, IImportValidator<T> validator, CancellationToken ct);
    Task<ImportCommitReceipt> CommitAsync<T>(ImportResult<T> preview, IImportCommitter<T> committer, CancellationToken ct);
}
