using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
namespace QuadroApp.ViewModels;

/// <summary>
/// CRUD ViewModel voor Klantenbeheer.
/// Doelen:
/// - Geen crashes (guards + try/catch + dialogs)
/// - IsBusy/IsLoading + CanExecute correct
/// - Fix: XAML item-buttons (CommandParameter) werken ook (overload methodes)
/// - Excel import robuust (IFilePickerService + null safe issues)
/// </summary>
public partial class KlantenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;

    private readonly KlantImportDefinition _klantImportDefinition;
    private readonly ICrudValidator<Klant> _validator;
    private readonly IKlantDialogService _klantDialog;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<Klant> klanten = new();
    [ObservableProperty] private ObservableCollection<Klant> filteredKlanten = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Klant? selectedKlant;

    [ObservableProperty] private string? foutmelding;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool hasChanges;

    public KlantenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        ICrudValidator<Klant> validator,
        IDialogService dialogs,
        IKlantDialogService klantDialog,
        KlantImportDefinition klantImportDefinition,
        IToastService toast)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _klantDialog = klantDialog ?? throw new ArgumentNullException(nameof(klantDialog));
        _klantImportDefinition = klantImportDefinition ?? throw new ArgumentNullException(nameof(klantImportDefinition));
        _toast = toast;
        _validator = validator;
    }
    protected async Task<bool> GuardAsync(
    Func<Task<ValidationResult>> validate,
    Func<Task> action,
    string? successMessage = null)
    {
        var vr = await validate();

        // warnings tonen (niet blokkeren)
        var warn = vr.WarningText();
        if (!string.IsNullOrWhiteSpace(warn))
            _toast.Warning(warn);

        // errors blokkeren
        if (!vr.IsValid)
        {
            _toast.Error(vr.ErrorText());
            return false;
        }

        try
        {
            IsBusy = true;
            await action();

            if (!string.IsNullOrWhiteSpace(successMessage))
                _toast.Success(successMessage);

            return true;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _toast.Error(msg);
            Foutmelding = msg;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string? value) => ApplyFilter();

    // ---------------- Excel import ----------------

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task ImportExcelAsync()
    {
        if (!CanRunAction()) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            var confirm = await _dialogs.ShowUnifiedImportPreviewAsync(_klantImportDefinition);
            if (confirm)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"Excel import mislukt: {msg}";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
            ImportExcelCommand.NotifyCanExecuteChanged();
            NewCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    // ---------------- Data loading / filtering ----------------

    private async Task LoadAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var list = await db.Klanten
                .AsNoTracking()
                .OrderBy(k => k.Achternaam)
                .ThenBy(k => k.Voornaam)
                .ToListAsync();

            Klanten = new ObservableCollection<Klant>(list);
            ApplyFilter();

            // Zorg dat selectie nooit naar "verwijderde" objecten wijst
            if (SelectedKlant is not null && FilteredKlanten.All(k => k.Id != SelectedKlant.Id))
                SelectedKlant = FilteredKlanten.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden: {ex.InnerException?.Message ?? ex.Message}";
            await _dialogs.ShowErrorAsync("Laden mislukt", Foutmelding);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (Klanten is null || Klanten.Count == 0)
        {
            FilteredKlanten = new ObservableCollection<Klant>();
            return;
        }

        if (string.IsNullOrWhiteSpace(Zoekterm))
        {
            FilteredKlanten = new ObservableCollection<Klant>(Klanten);
            return;
        }

        var t = Zoekterm.Trim().ToLowerInvariant();

        var q = Klanten.Where(k =>
            (k.Voornaam ?? "").ToLowerInvariant().Contains(t) ||
            (k.Achternaam ?? "").ToLowerInvariant().Contains(t) ||
            (k.Email ?? "").ToLowerInvariant().Contains(t) ||
            (k.Gemeente ?? "").ToLowerInvariant().Contains(t) ||
            (k.Straat ?? "").ToLowerInvariant().Contains(t) ||
            (k.Telefoon ?? "").ToLowerInvariant().Contains(t) ||
            (k.Opmerking ?? "").ToLowerInvariant().Contains(t) ||
            (k.BtwNummer ?? "").ToLowerInvariant().Contains(t));

        FilteredKlanten = new ObservableCollection<Klant>(q);
    }

    // ---------------- Commands ----------------

    private bool CanRunAction() => !IsBusy && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task GaTerugAsync()
    {
        if (!CanRunAction()) return;

        try
        {
            await _nav.NavigateToAsync<HomeViewModel>();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Navigatie mislukt: {ex.InnerException?.Message ?? ex.Message}";
            await _dialogs.ShowErrorAsync("Navigatie", Foutmelding);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task OpenKlantDetailAsync(Klant? klant)
    {
        if (!CanRunAction()) return;

        var target = klant ?? SelectedKlant;
        if (target is null) return;

        await _nav.NavigateToKlantDetailAsync(target.Id);
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task RefreshAsync()
    {
        if (!CanRunAction()) return;
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task NewAsync()
    {
        if (!CanRunAction()) return;

        var result = await _klantDialog.EditAsync(new Klant
        {
            Voornaam = "",
            Achternaam = ""
        });

        if (result is null) return;

        await GuardAsync(
            () => _validator.ValidateCreateAsync(result),
            async () =>
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                db.Klanten.Add(result);
                await db.SaveChangesAsync();

                HasChanges = false;
                Klanten.Add(result);
                ApplyFilter();
                SelectedKlant = result;
            },
            successMessage: "Klant aangemaakt."
        );

        ImportExcelCommand.NotifyCanExecuteChanged();
        NewCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private bool CanEdit() => SelectedKlant is not null && CanRunAction();
    private bool CanDelete() => SelectedKlant is not null && CanRunAction();

    // Button in itemtemplate gebruikt CommandParameter -> overload nodig


    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync(Klant? klant)
    {
        if (klant is null || !CanRunAction()) return;

        var kopie = Clone(klant);
        var result = await _klantDialog.EditAsync(kopie);
        if (result is null) return;

        await GuardAsync(
            () => _validator.ValidateUpdateAsync(result),
            async () =>
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                db.Attach(result);
                db.Entry(result).State = EntityState.Modified;
                await db.SaveChangesAsync();

                ReplaceInCollections(result);
                HasChanges = false;
            },
            successMessage: "Klant aangepast."
        );

        ImportExcelCommand.NotifyCanExecuteChanged();
        NewCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    // Button in itemtemplate gebruikt CommandParameter -> overload nodig


    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync(Klant? klant)
    {
        if (klant is null || !CanRunAction()) return;

        var displayNaam = $"{klant.Voornaam} {klant.Achternaam}".Trim();

        var ok = await _dialogs.ConfirmAsync(
            "Klant verwijderen",
            $"Ben je zeker dat je {displayNaam} wil verwijderen?"
        );

        if (!ok) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            db.Klanten.Remove(new Klant { Id = klant.Id });
            await db.SaveChangesAsync();

            var id = klant.Id;

            // UI verwijderen
            var inAll = Klanten.FirstOrDefault(k => k.Id == id);
            if (inAll is not null) Klanten.Remove(inAll);

            var inFiltered = FilteredKlanten.FirstOrDefault(k => k.Id == id);
            if (inFiltered is not null) FilteredKlanten.Remove(inFiltered);

            if (SelectedKlant?.Id == id)
                SelectedKlant = null;

            HasChanges = false;
        }
        catch (DbUpdateException dbex)
        {
            // Vaak FK constraint (klant zit in offertes)
            Foutmelding = $"❌ Verwijderen mislukt: {dbex.InnerException?.Message ?? dbex.Message}";
            await _dialogs.ShowErrorAsync("Klant verwijderen mislukt",
                "Deze klant kan niet verwijderd worden omdat hij nog gekoppeld is aan offertes/werkbonnen.\n\n" + Foutmelding);
        }
        catch (Exception ex)
        {
            Foutmelding = $"❌ Verwijderen mislukt: {ex.InnerException?.Message ?? ex.Message}";
            await _dialogs.ShowErrorAsync("Klant verwijderen mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            ImportExcelCommand.NotifyCanExecuteChanged();
            NewCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task SaveAsync()
    {
        if (!CanRunAction()) return;
        await LoadAsync();
        HasChanges = false;
    }

    public void MarkDirty() => HasChanges = true;

    // ---------------- Validation ----------------

    private static bool ValidateKlant(Klant k, out string message)
    {
        // Minimale demo-proof validaties (pas gerust aan)
        if (string.IsNullOrWhiteSpace(k.Achternaam) && string.IsNullOrWhiteSpace(k.Voornaam))
        {
            message = "Voornaam of achternaam is verplicht.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(k.Email) && !k.Email.Contains('@', StringComparison.Ordinal))
        {
            message = "Email adres lijkt ongeldig.";
            return false;
        }

        // Postcode/nummer zijn vaak int? -> laat leeg toe; geen harde fout
        message = "";
        return true;
    }

    // ---------------- Helpers ----------------

    private static Klant Clone(Klant k) => new()
    {
        Id = k.Id,
        Voornaam = k.Voornaam,
        Achternaam = k.Achternaam,
        Email = k.Email,
        Telefoon = k.Telefoon,
        Straat = k.Straat,
        Nummer = k.Nummer,
        Postcode = k.Postcode,
        Gemeente = k.Gemeente,
        Opmerking = k.Opmerking,
        BtwNummer = k.BtwNummer
    };

    private void ReplaceInCollections(Klant updated)
    {
        var idxAll = Klanten
            .Select((k, i) => (k, i))
            .FirstOrDefault(x => x.k.Id == updated.Id);

        if (idxAll.k is not null)
            Klanten[idxAll.i] = updated;

        var idxFiltered = FilteredKlanten
            .Select((k, i) => (k, i))
            .FirstOrDefault(x => x.k.Id == updated.Id);

        if (idxFiltered.k is not null)
            FilteredKlanten[idxFiltered.i] = updated;

        SelectedKlant = updated;
        ApplyFilter();
    }
}