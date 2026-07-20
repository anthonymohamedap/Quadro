using System;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using QuadroApp.Service;
using QuadroApp.Service.Model;

var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
var sut = new CentralExcelExportService(sqlite.Factory);
var cfg = await sut.MaakConfiguratieAsync(ExcelExportDataset.Lijsten, "voorraadoverzicht");
Console.WriteLine($"Relaties:{cfg.Relaties.Count}");
foreach (var r in cfg.Relaties) Console.WriteLine($"{r.Sleutel}|{r.Label}|sel={r.IsGeselecteerd}|cols={r.Kolommen.Count}");
