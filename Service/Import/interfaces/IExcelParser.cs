using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;

public interface IExcelParser
{
    Task<IReadOnlyList<Dictionary<string, string?>>> ReadSheetAsync(Stream stream, string sheetName, CancellationToken ct);
}
