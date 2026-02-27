using ClosedXML.Excel;
using QuadroApp.Model.Import;
using System.Text.RegularExpressions;

namespace QuadroApp.Service.Import
{
    public static class KlantExcelRowMapper
    {
        public static KlantPreviewRow Map(IXLRow r, int rowNumber)
        {
            static string? Opt(IXLRow row, int col)
            {
                var s = row.Cell(col).GetString().Trim();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }

            var row = new KlantPreviewRow
            {
                RowNumber = rowNumber,
                Voornaam = Opt(r, 1) ?? "",
                Achternaam = Opt(r, 2) ?? "",
                Email = Opt(r, 3),
                Telefoon = Opt(r, 4),
                Straat = Opt(r, 5),
                Nummer = Opt(r, 6),
                Postcode = Opt(r, 7),
                Gemeente = Opt(r, 8),
                BtwNummer = Opt(r, 9),
                Opmerking = Opt(r, 10)
            };

            // ✅ Preview rules (licht)
            if (string.IsNullOrWhiteSpace(row.Voornaam) && string.IsNullOrWhiteSpace(row.Achternaam))
            {
                row.IsValid = false;
                row.ValidationMessage = "Voornaam of achternaam is verplicht";
                return row;
            }

            if (!string.IsNullOrWhiteSpace(row.Email) &&
                !Regex.IsMatch(row.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                row.IsValid = false;
                row.ValidationMessage = "Email is ongeldig";
                return row;
            }

            if (!string.IsNullOrWhiteSpace(row.Postcode) &&
                !Regex.IsMatch(row.Postcode, @"^\d{4}$"))
            {
                row.IsValid = false;
                row.ValidationMessage = "Postcode moet 4 cijfers zijn";
                return row;
            }

            // BTW: als ingevuld -> moet BE + 10 cijfers of 10 cijfers zijn
            if (!string.IsNullOrWhiteSpace(row.BtwNummer))
            {
                var btw = row.BtwNummer.Replace(" ", "").Replace(".", "").Replace("-", "");
                if (btw.StartsWith("BE", System.StringComparison.OrdinalIgnoreCase))
                    btw = btw[2..];

                if (!Regex.IsMatch(btw, @"^\d{10}$"))
                {
                    row.IsValid = false;
                    row.ValidationMessage = "BTW-nummer ongeldig (verwacht BE + 10 cijfers)";
                    return row;
                }
            }

            row.IsValid = true;
            row.ValidationMessage = "OK";
            return row;
        }
    }
}