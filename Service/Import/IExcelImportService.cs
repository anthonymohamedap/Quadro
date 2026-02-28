// Candidate for removal – requires runtime verification
﻿

using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;

[Obsolete("Not used in current startup flow. Remove after runtime verification.")]
public interface IExcelImportService
{
    Task<ImportPreviewResult> ReadTypeLijstenPreviewAsync(string filePath);
    Task<ImportCommitResult> CommitTypeLijstenAsync(IEnumerable<TypeLijstPreviewRow> rows);
}
