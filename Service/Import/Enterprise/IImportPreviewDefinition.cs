using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public interface IImportPreviewDefinition
{
    string EntityName { get; }
    Task<QuadroApp.Model.Import.ImportResult<object>> DryRunAsync(Stream stream, CancellationToken ct);
    Task<ImportCommitReceipt> CommitAsync(QuadroApp.Model.Import.ImportResult<object> preview, CancellationToken ct);
    IReadOnlyDictionary<string, string?> ToDisplayMap(object item);
    string GetItemKey(object item);
}
