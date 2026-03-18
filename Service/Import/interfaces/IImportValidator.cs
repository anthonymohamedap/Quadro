using QuadroApp.Data;
using QuadroApp.Model.Import;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public interface IImportValidator<T>
{
    Task ValidateAsync(ImportRowResult<T> row, AppDbContext db, CancellationToken ct);
}
