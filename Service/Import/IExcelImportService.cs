

using QuadroApp.Model.Import;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Services.Import;

public interface IExcelImportService
{
    Task<ImportPreviewResult> ReadTypeLijstenPreviewAsync(string filePath);
    Task<ImportCommitResult> CommitTypeLijstenAsync(IEnumerable<TypeLijstPreviewRow> rows);
}

