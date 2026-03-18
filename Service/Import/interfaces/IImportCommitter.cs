using QuadroApp.Data;
using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public interface IImportCommitter<T>
{
    Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<T>> validRows, AppDbContext db, CancellationToken ct);
}
