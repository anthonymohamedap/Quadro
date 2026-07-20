using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class KlantSelectieViewModel : AsyncViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICrudValidator<Klant> _klantValidator;
    private readonly IKlantDialogService _klantDialog;
    private readonly IDialogService _dialogs;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string? klantZoekterm;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private ObservableCollection<Klant> klanten = new();

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private ObservableCollection<Klant> gefilterdeKlanten = new();

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private Klant? selectedKlant;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool isBusy;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string? foutmelding;

    /// <summary>
    /// Vuurt wanneer de gebruiker een klant selecteert of aanmaakt.
    /// OfferteViewModel abonneert hier op om Offerte.KlantId te updaten.
    /// </summary>
    public event Action<Klant?>? KlantSelected;

    public IAsyncRelayCommand NieuweKlantCommand { get; }

    public KlantSelectieViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        ICrudValidator<Klant> klantValidator,
        IKlantDialogService klantDialog,
        IDialogService dialogs,
        IToastService toast)
        : base(toast)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _klantValidator = klantValidator ?? throw new ArgumentNullException(nameof(klantValidator));
        _klantDialog = klantDialog ?? throw new ArgumentNullException(nameof(klantDialog));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        NieuweKlantCommand = new AsyncRelayCommand(NieuweKlantAsync, () => !IsBusy);
    }

    partial void OnKlantZoektermChanged(string? value) => FilterKlanten();

    partial void OnSelectedKlantChanged(Klant? value) => KlantSelected?.Invoke(value);

    public void SetKlanten(System.Collections.Generic.IEnumerable<Klant> klanten, int? selectedKlantId)
    {
        Klanten.Clear();
        foreach (var k in klanten)
            Klanten.Add(k);

        FilterKlanten();

        if (selectedKlantId.HasValue)
            SelectedKlant = Klanten.FirstOrDefault(k => k.Id == selectedKlantId.Value);
    }

    public void FilterKlanten()
    {
        var term = KlantZoekterm?.Trim();

        var lijst = string.IsNullOrWhiteSpace(term)
            ? (System.Collections.Generic.IEnumerable<Klant>)Klanten
            : Klanten.Where(k =>
                (k.Voornaam ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (k.Achternaam ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (k.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));

        GefilterdeKlanten.Clear();
        foreach (var k in lijst)
            GefilterdeKlanten.Add(k);
    }

    private async Task NieuweKlantAsync()
    {
        try
        {
            IsBusy = true;
            Foutmelding = null;

            var nieuw = new Klant
            {
                Voornaam = "", Achternaam = "", Email = null, Telefoon = null,
                Straat = null, Nummer = null, Postcode = null, Gemeente = null,
                BtwNummer = null, Opmerking = null
            };

            var ingevuld = await _klantDialog.EditAsync(nieuw);
            if (ingevuld is null)
            {
                Toast.Info("Aanmaken geannuleerd.");
                return;
            }

            ingevuld.Voornaam = (ingevuld.Voornaam ?? "").Trim();
            ingevuld.Achternaam = (ingevuld.Achternaam ?? "").Trim();
            ingevuld.Email = string.IsNullOrWhiteSpace(ingevuld.Email) ? null : ingevuld.Email.Trim();
            ingevuld.Telefoon = string.IsNullOrWhiteSpace(ingevuld.Telefoon) ? null : ingevuld.Telefoon.Trim();
            ingevuld.Straat = string.IsNullOrWhiteSpace(ingevuld.Straat) ? null : ingevuld.Straat.Trim();
            ingevuld.Nummer = string.IsNullOrWhiteSpace(ingevuld.Nummer) ? null : ingevuld.Nummer.Trim();
            ingevuld.Postcode = string.IsNullOrWhiteSpace(ingevuld.Postcode) ? null : ingevuld.Postcode.Trim();
            ingevuld.Gemeente = string.IsNullOrWhiteSpace(ingevuld.Gemeente) ? null : ingevuld.Gemeente.Trim();
            ingevuld.BtwNummer = string.IsNullOrWhiteSpace(ingevuld.BtwNummer) ? null : ingevuld.BtwNummer.Trim();
            ingevuld.Opmerking = string.IsNullOrWhiteSpace(ingevuld.Opmerking) ? null : ingevuld.Opmerking.Trim();

            var vr = await _klantValidator.ValidateCreateAsync(ingevuld);

            var warn = vr.WarningText();
            if (!string.IsNullOrWhiteSpace(warn))
                Toast.Warning(warn);

            if (!vr.IsValid)
            {
                var err = vr.ErrorText();
                Toast.Error(string.IsNullOrWhiteSpace(err) ? vr.ToString() : err);
                return;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            db.Klanten.Add(ingevuld);
            await db.SaveChangesAsync();

            Klanten.Add(ingevuld);
            FilterKlanten();
            SelectedKlant = ingevuld; // fires KlantSelected via OnSelectedKlantChanged

            Toast.Success("Klant aangemaakt en geselecteerd.");
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            Toast.Error($"Klant aanmaken mislukt: {Foutmelding}");
            await _dialogs.ShowErrorAsync("Klant aanmaken mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            NieuweKlantCommand.NotifyCanExecuteChanged();
        }
    }
}
