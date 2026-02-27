using Avalonia.Controls;
using Avalonia.Interactivity;
using QuadroApp.Model.DB;

namespace QuadroApp.Views
{
    public partial class KlantDialog : Window
    {
        public Klant? Result { get; private set; }

        public KlantDialog()
        {
            InitializeComponent();
        }

        public KlantDialog(Klant klant) : this()
        {
            // ✅ Zorg dat Voornaam/Achternaam nooit null worden (entity heeft non-nullable strings)
            DataContext = new Klant
            {
                Id = klant.Id,
                Voornaam = klant.Voornaam ?? "",
                Achternaam = klant.Achternaam ?? "",

                Email = klant.Email,
                Telefoon = klant.Telefoon,
                Straat = klant.Straat,
                Nummer = klant.Nummer,
                Postcode = klant.Postcode,
                Gemeente = klant.Gemeente,

                BtwNummer = klant.BtwNummer,
                Opmerking = klant.Opmerking
            };
        }

        private void Annuleren_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void Opslaan_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not Klant k)
            {
                Close(null);
                return;
            }

            // ✅ kleine cleanup/trimming zodat validator en DB netjes zijn
            k.Voornaam = (k.Voornaam ?? "").Trim();
            k.Achternaam = (k.Achternaam ?? "").Trim();

            k.Email = string.IsNullOrWhiteSpace(k.Email) ? null : k.Email.Trim();
            k.Telefoon = string.IsNullOrWhiteSpace(k.Telefoon) ? null : k.Telefoon.Trim();
            k.Straat = string.IsNullOrWhiteSpace(k.Straat) ? null : k.Straat.Trim();
            k.Nummer = string.IsNullOrWhiteSpace(k.Nummer) ? null : k.Nummer.Trim();
            k.Postcode = string.IsNullOrWhiteSpace(k.Postcode) ? null : k.Postcode.Trim();
            k.Gemeente = string.IsNullOrWhiteSpace(k.Gemeente) ? null : k.Gemeente.Trim();
            k.BtwNummer = string.IsNullOrWhiteSpace(k.BtwNummer) ? null : k.BtwNummer.Trim();
            k.Opmerking = string.IsNullOrWhiteSpace(k.Opmerking) ? null : k.Opmerking.Trim();

            Result = k;
            Close(Result);
        }
    }
}