using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class ClosedXmlExcelParser : IExcelParser
{
    public Task<IReadOnlyList<Dictionary<string, string?>>> ReadSheetAsync(Stream stream, string sheetName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var rows = new List<Dictionary<string, string?>>();
        var headerRow = worksheet.Row(1);
        var headers = headerRow.CellsUsed().Select(c => c.GetString().Trim()).ToList();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowIndex);
            if (row.IsEmpty())
            {
                continue;
            }

            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < headers.Count; col++)
            {
                var key = headers[col];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                data[key] = row.Cell(col + 1).GetString();
            }

            data["__RowNumber"] = rowIndex.ToString();
            rows.Add(data);
        }

        return Task.FromResult<IReadOnlyList<Dictionary<string, string?>>>(rows);
    }
}
