using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    private static DatasetDefinitie BuildAfwerkingenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Afwerkingen,
            "Afwerkingen",
            "Export van afwerkingsfamilies en kleurvarianten.",
            "Afwerkingen",
            "afwerkingen-export",
            async db => (await db.AfwerkingsOpties.AsNoTracking()
                .Include(x => x.AfwerkingsGroep)
                .Include(x => x.Leverancier)
                .OrderBy(x => x.AfwerkingsGroep.Code)
                .ThenBy(x => x.Volgnummer)
                .ThenBy(x => x.Kleur)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((AfwerkingsOptie)row).Id,
            row => $"{((AfwerkingsOptie)row).AfwerkingsGroep.Naam} - {((AfwerkingsOptie)row).Naam}",
            row => ((AfwerkingsOptie)row).Kleur,
            [
                Col("id", "Id", "Basis", row => ((AfwerkingsOptie)row).Id, false),
                Col("groepCode", "Groepcode", "Basis", row => ((AfwerkingsOptie)row).AfwerkingsGroep.Code.ToString()),
                Col("groepNaam", "Groep", "Basis", row => ((AfwerkingsOptie)row).AfwerkingsGroep.Naam),
                Col("familie", "Familie", "Familie", row => ((AfwerkingsOptie)row).Volgnummer.ToString()),
                Col("kleur", "Kleur", "Familie", row => ((AfwerkingsOptie)row).Kleur),
                Col("naam", "Naam", "Familie", row => ((AfwerkingsOptie)row).Naam),
                Col("leverancier", "Leverancier", "Leverancier", row => ((AfwerkingsOptie)row).Leverancier?.Naam ?? string.Empty),
                Col("kostprijs", "Kostprijs per m²", "Prijs", row => ((AfwerkingsOptie)row).KostprijsPerM2),
                Col("winstmarge", "Winstmarge", "Prijs", row => ((AfwerkingsOptie)row).WinstMarge),
                Col("afval", "Afvalpercentage", "Prijs", row => ((AfwerkingsOptie)row).AfvalPercentage),
                Col("vasteKost", "Vaste kost", "Prijs", row => ((AfwerkingsOptie)row).VasteKost),
                Col("werkMinuten", "Werkminuten", "Prijs", row => ((AfwerkingsOptie)row).WerkMinuten)
            ],
            []);
    }
}
