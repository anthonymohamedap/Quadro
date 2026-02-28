using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
namespace QuadroApp.ViewModels;

public partial class AfwerkingenViewModel : ObservableObject
{
    private readonly IAfwerkingenService _service;
    private readonly INavigationService _nav;
    private readonly AfwerkingsOptieImportDefinition _afwerkingsImportDefinition;
    private readonly IDialogService _dialogs;
    private readonly ICrudValidator<AfwerkingsOptie> _validator;
    private readonly IToastService _toast;
    public bool HeeftGeenSelectie => SelectedOptie is null;
    // ─────────────────────────────────────────────────────────────
    // Busy/Status
    // ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string? foutmelding;
    [ObservableProperty] private bool hasChanges;

    // ─────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<AfwerkingsGroep> groepen = new();
    [ObservableProperty] private AfwerkingsGroep? selectedGroep;

    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> opties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> filteredOpties = new();

    [ObservableProperty] private ObservableCollection<Leverancier> leveranciers = new();
    [ObservableProperty] private Leverancier? selectedLeverancier;

    [ObservableProperty] private string? zoekterm;

    [ObservableProperty] private decimal previewBreedteCm = 30;
    [ObservableProperty] private decimal previewHoogteCm = 40;

    [ObservableProperty] private bool isDealerOptie;

    [ObservableProperty] private string? leverancierZoekterm;
    [ObservableProperty]
    private ObservableCollection<Leverancier> gefilterdeLeveranciers = new();

    private bool isSynchronizing;

    private AfwerkingsOptie? selectedOptie;
    public AfwerkingsOptie? SelectedOptie
    {
        get => selectedOptie;
        set
        {
            if (SetProperty(ref selectedOptie, value))
            {
                DeleteAsyncCommand.NotifyCanExecuteChanged();
                SaveAsyncCommand.NotifyCanExecuteChanged();
                SyncSelectedOptieBindings();
                OnPropertyChanged(nameof(PreviewPrijsText));
                OnPropertyChanged(nameof(HeeftGeenSelectie));
            }
        }
    }

    public string AantalOptiesText => $"{FilteredOpties.Count} opties";
    public bool HeeftSelectie => SelectedOptie is not null;

    // ─────────────────────────────────────────────────────────────
    // Commands
    // ─────────────────────────────────────────────────────────────
    public IAsyncRelayCommand RefreshAsyncCommand { get; }
    public IAsyncRelayCommand SaveAsyncCommand { get; }
    public IAsyncRelayCommand NewAsyncCommand { get; }
    public IAsyncRelayCommand DeleteAsyncCommand { get; }


    // ─────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────
    public AfwerkingenViewModel(
        IAfwerkingenService service,
        INavigationService nav,
        AfwerkingsOptieImportDefinition afwerkingsImportDefinition,
        IDialogService dialogs,
        ICrudValidator<AfwerkingsOptie> validator,
    IToastService toast)
    {
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _afwerkingsImportDefinition = afwerkingsImportDefinition ?? throw new ArgumentNullException(nameof(afwerkingsImportDefinition));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _validator = validator;
        _toast = toast;
        RefreshAsyncCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SaveAsyncCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        NewAsyncCommand = new AsyncRelayCommand(NewAsync, () => !IsBusy);
        DeleteAsyncCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);

        _ = LoadAsync();
    }
    public string VolgnummerText
    {
        get => SelectedOptie is null
            ? ""
            : SelectedOptie.Volgnummer.ToString();

        set
        {
            if (SelectedOptie is null)
                return;

            if (string.IsNullOrWhiteSpace(value))
                return;

            var c = char.ToUpperInvariant(value.Trim()[0]);

            // enkel 1-9 of A-K
            if ((c >= '1' && c <= '9') || (c >= 'A' && c <= 'K'))
            {
                SelectedOptie.Volgnummer = c;
                HasChanges = true;
            }

            OnPropertyChanged();
        }
    }
    // ─────────────────────────────────────────────────────────────
    // Import Excel
    // ─────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanImportExcel))]
    private async Task ImportExcelAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Status = "Excel import…";
            Foutmelding = null;

            var confirm = await _dialogs.ShowUnifiedImportPreviewAsync(_afwerkingsImportDefinition);
            if (!confirm)
            {
                Status = "Geannuleerd";
                return;
            }

            await LoadAsync();
            _toast.Success("Import van afwerkingen voltooid.");
            Status = "Import klaar";
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"Excel import mislukt: {msg}";
            Status = "Fout";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
            RefreshAsyncCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            NewAsyncCommand.NotifyCanExecuteChanged();
            DeleteAsyncCommand.NotifyCanExecuteChanged();
            ImportExcelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanImportExcel() => !IsBusy;

    // ─────────────────────────────────────────────────────────────
    // Data loading
    // ─────────────────────────────────────────────────────────────
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Status = "Laden…";
            Foutmelding = null;

            var groepen = await _service.GetGroepenAsync();
            var leveranciers = await _service.GetLeveranciersAsync();

            Groepen = new ObservableCollection<AfwerkingsGroep>(groepen ?? new List<AfwerkingsGroep>());
            Leveranciers = new ObservableCollection<Leverancier>(leveranciers ?? new List<Leverancier>());

            // init leveranciersfilter
            FilterLeveranciers();

            // behoud selectie indien mogelijk
            if (SelectedGroep is null && Groepen.Any())
                SelectedGroep = Groepen.FirstOrDefault();
            else if (SelectedGroep is not null && Groepen.All(g => g.Id != SelectedGroep.Id))
                SelectedGroep = Groepen.FirstOrDefault();

            await LoadOptiesAsync();
            Status = "Klaar";
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden: {ex.InnerException?.Message ?? ex.Message}";
            Status = "Fout";
        }
        finally
        {
            IsBusy = false;
            RefreshAsyncCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            NewAsyncCommand.NotifyCanExecuteChanged();
            DeleteAsyncCommand.NotifyCanExecuteChanged();
            ImportExcelCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task LoadOptiesAsync()
    {
        try
        {
            var groepId = SelectedGroep?.Id;
            var opties = await _service.GetOptiesAsync(groepId);
            Opties = new ObservableCollection<AfwerkingsOptie>(opties ?? new List<AfwerkingsOptie>());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden van opties: {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Filtering
    // ─────────────────────────────────────────────────────────────
    private void ApplyFilter()
    {
        var vorigeSelectieId = SelectedOptie?.Id;

        var src = Opties.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(Zoekterm))
        {
            var t = Zoekterm.Trim();
            src = src.Where(o =>
                o.Volgnummer.ToString().Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (o.Naam ?? "").Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        FilteredOpties = new ObservableCollection<AfwerkingsOptie>(src);
        OnPropertyChanged(nameof(AantalOptiesText));

        SelectedOptie = vorigeSelectieId.HasValue
            ? FilteredOpties.FirstOrDefault(x => x.Id == vorigeSelectieId)
            : FilteredOpties.FirstOrDefault();
    }

    private void SyncSelectedOptieBindings()
    {
        isSynchronizing = true;
        try
        {
            if (SelectedOptie is null)
            {
                SelectedLeverancier = null;
                IsDealerOptie = false;
                return;
            }

            IsDealerOptie = string.Equals(
                SelectedOptie.Leverancier?.Naam,
                "DLR",
                StringComparison.OrdinalIgnoreCase);

            SelectedLeverancier = SelectedOptie.LeverancierId.HasValue
                ? Leveranciers.FirstOrDefault(l => l.Id == SelectedOptie.LeverancierId.Value)
                : null;
        }
        finally
        {
            isSynchronizing = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Preview prijs (veilig)
    // ─────────────────────────────────────────────────────────────
    public string PreviewPrijsText
    {
        get
        {
            var o = SelectedOptie;
            if (o is null) return "Selecteer een optie voor preview.";

            if (PreviewBreedteCm <= 0 || PreviewHoogteCm <= 0)
                return "Afmetingen ongeldig.";

            var m2 = (PreviewBreedteCm * PreviewHoogteCm) / 10_000m;
            if (m2 <= 0) return "Afmetingen ongeldig.";

            // guard tegen negatieve input uit DB
            var kostprijsM2 = o.KostprijsPerM2 < 0 ? 0 : o.KostprijsPerM2;
            var vasteKost = o.VasteKost < 0 ? 0 : o.VasteKost;
            var afvalPct = o.AfvalPercentage < 0 ? 0 : o.AfvalPercentage;
            var winstmarge = o.WinstMarge < 0 ? 0 : o.WinstMarge;

            var kost = kostprijsM2 * m2 + vasteKost;
            var afval = kost * (afvalPct / 100m);
            var excl = (kost + afval) * (1m + winstmarge);

            return $"Preview: € {excl:F2} excl. btw (voor {PreviewBreedteCm}×{PreviewHoogteCm} cm)";
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Commands
    // ─────────────────────────────────────────────────────────────
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        try
        {
            await _nav.NavigateToAsync<HomeViewModel>();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Navigatie mislukt: {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    private bool CanSave()
        => !IsBusy && SelectedOptie is not null;

    private async Task SaveAsync()
    {
        if (IsBusy) return;

        try
        {
            Foutmelding = null;

            if (SelectedOptie is null)
            {
                _toast.Warning("Selecteer eerst een afwerkingsoptie.");
                return;
            }

            IsBusy = true;
            Status = "Opslaan…";

            // Sync leverancier (zoals je al doet)
            SelectedOptie.LeverancierId = SelectedLeverancier?.Id;
            SelectedOptie.Leverancier = SelectedLeverancier;

            // ✅ centrale validatie
            var vr = await _validator.ValidateUpdateAsync(SelectedOptie);

            var warn = vr.WarningText();
            if (!string.IsNullOrWhiteSpace(warn))
                _toast.Warning(warn);

            if (!vr.IsValid)
            {
                _toast.Error(vr.ErrorText());
                Status = "Validatie fout";
                return;
            }

            await _service.SaveOptieAsync(SelectedOptie);

            HasChanges = false;
            await LoadOptiesAsync();

            Status = "Opgeslagen";
            _toast.Success("Afwerkingsoptie opgeslagen.");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"Opslaan mislukt: {msg}";
            Status = "Fout";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
            RefreshAsyncCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            NewAsyncCommand.NotifyCanExecuteChanged();
            DeleteAsyncCommand.NotifyCanExecuteChanged();
            ImportExcelCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task NewAsync()
    {
        if (IsBusy) return;

        try
        {
            Foutmelding = null;

            if (SelectedGroep is null)
            {
                await _dialogs.ShowErrorAsync("Nieuwe optie", "Selecteer eerst een groep.");
                return;
            }

            IsBusy = true;
            Status = "Nieuwe optie…";

            var nieuw = await _service.CreateNieuweOptieAsync(SelectedGroep.Id);

            await LoadOptiesAsync();
            SelectedOptie = FilteredOpties.FirstOrDefault(x => x.Id == nieuw.Id) ?? FilteredOpties.FirstOrDefault();

            HasChanges = true;
            Status = "Klaar";
        }
        catch (Exception ex)
        {
            Foutmelding = $"Nieuwe optie mislukt: {ex.InnerException?.Message ?? ex.Message}";
            Status = "Fout";
            await _dialogs.ShowErrorAsync("Nieuwe optie", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            RefreshAsyncCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            NewAsyncCommand.NotifyCanExecuteChanged();
            DeleteAsyncCommand.NotifyCanExecuteChanged();
            ImportExcelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDelete()
        => !IsBusy && SelectedOptie is not null;

    private async Task DeleteAsync()
    {
        if (IsBusy) return;

        try
        {
            if (SelectedOptie is null)
            {
                await _dialogs.ShowErrorAsync("Verwijderen", "Selecteer eerst een afwerkingsoptie.");
                return;
            }

            IsBusy = true;
            Status = "Verwijderen…";
            Foutmelding = null;

            await _service.DeleteOptieAsync(SelectedOptie);

            await LoadOptiesAsync();
            HasChanges = true;

            Status = "Verwijderd";
        }
        catch (Exception ex)
        {
            Foutmelding = $"Verwijderen mislukt: {ex.InnerException?.Message ?? ex.Message}";
            Status = "Fout";
            await _dialogs.ShowErrorAsync("Verwijderen", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            RefreshAsyncCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            NewAsyncCommand.NotifyCanExecuteChanged();
            DeleteAsyncCommand.NotifyCanExecuteChanged();
            ImportExcelCommand.NotifyCanExecuteChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Property hooks
    // ─────────────────────────────────────────────────────────────
    partial void OnZoektermChanged(string? value) => ApplyFilter();

    partial void OnSelectedGroepChanged(AfwerkingsGroep? value)
    {
        if (IsBusy) return;
        _ = LoadOptiesAsync();
    }

    partial void OnPreviewBreedteCmChanged(decimal value)
        => OnPropertyChanged(nameof(PreviewPrijsText));

    partial void OnPreviewHoogteCmChanged(decimal value)
        => OnPropertyChanged(nameof(PreviewPrijsText));

    partial void OnSelectedLeverancierChanged(Leverancier? value)
    {
        if (SelectedOptie is null || isSynchronizing) return;

        SelectedOptie.LeverancierId = value?.Id;
        SelectedOptie.Leverancier = value;
        HasChanges = true;
        SaveAsyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnLeverancierZoektermChanged(string? value)
    {
        // laat de huidige binding update afwerken
        Avalonia.Threading.Dispatcher.UIThread.Post(FilterLeveranciers);
    }

    private void FilterLeveranciers()
    {
        if (Leveranciers is null)
            return;

        var term = LeverancierZoekterm?.Trim();

        var lijst = string.IsNullOrWhiteSpace(term)
            ? Leveranciers.ToList()
            : Leveranciers
                .Where(l =>
                    (l.Naam ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // BELANGRIJK: ItemsSource niet vervangen -> enkel inhoud updaten
        GefilterdeLeveranciers.Clear();
        foreach (var l in lijst)
            GefilterdeLeveranciers.Add(l);
    }
}