using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuadroApp.ViewModels;

public partial class ExportCenterViewModel : ObservableObject, IAsyncInitializable
{
    private readonly INavigationService _nav;
    private readonly ICentralExcelExportService _exportService;
    private readonly IFilePickerService _filePicker;
    private readonly IPathOpener _pathOpener;
    private readonly IAppSettingsProvider _settings;
    private readonly IToastService _toast;
    private bool _selectionEventsSuppressed;
    private bool _isLoadingConfiguration;
    private bool _presetHandmatigAangepast;

    private const int PaginaGrootte = 20;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChooseExportFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExporteerConfiguratieCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelecteerAlleVeldenCommand))]
    [NotifyCanExecuteChangedFor(nameof(WisVeldenCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelecteerAlleEntiteitenCommand))]
    [NotifyCanExecuteChangedFor(nameof(WisEntiteitenCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenExportFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenLastExportCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenExportFolderCommand))]
    private string exportFolder = GetDefaultExportFolder();

    [ObservableProperty] private string statusMessage = "Kies een dataset of preset en selecteer daarna de gewenste velden.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastExportPath))]
    [NotifyCanExecuteChangedFor(nameof(OpenLastExportCommand))]
    private string? lastExportPath;

    [ObservableProperty] private string geselecteerdePresetBeschrijving = string.Empty;
    [ObservableProperty] private string datasetBeschrijving = string.Empty;

    [ObservableProperty] private ObservableCollection<ExportDatasetOptie> beschikbareDatasets = new();
    [ObservableProperty] private ObservableCollection<ExportPresetOptie> beschikbarePresets = new();
    [ObservableProperty] private ObservableCollection<ExportEntiteitOptie> beschikbareEntiteiten = new();
    [ObservableProperty] private ObservableCollection<ExportKolomOptie> beschikbareKolommen = new();
    [ObservableProperty] private ObservableCollection<ExportRelatieOptie> beschikbareRelaties = new();
    [ObservableProperty] private ExportDatasetOptie? selectedDataset;
    [ObservableProperty] private ExportPresetOptie? selectedPreset;

    [ObservableProperty] private string entiteitZoekterm = string.Empty;
    [ObservableProperty] private int huidigePagina = 1;
    [ObservableProperty] private ObservableCollection<ExportEntiteitOptie> gepagineerdeEntiteiten = new();

    public ExportCenterViewModel(
        INavigationService nav,
        ICentralExcelExportService exportService,
        IFilePickerService filePicker,
        IPathOpener pathOpener,
        IAppSettingsProvider settings,
        IToastService toast)
    {
        _nav = nav;
        _exportService = exportService;
        _filePicker = filePicker;
        _pathOpener = pathOpener;
        _settings = settings;
        _toast = toast;
    }

    public bool HasLastExportPath => !string.IsNullOrWhiteSpace(LastExportPath);
    public bool HeeftRelaties => BeschikbareRelaties.Count > 0;
    public bool GeenRelaties => !HeeftRelaties;
    public bool HeeftEntiteiten => BeschikbareEntiteiten.Count > 0;
    public bool GeenEntiteiten => !HeeftEntiteiten;
    public string GeselecteerdeVeldenTekst => $"{BeschikbareKolommen.Count(x => x.IsGeselecteerd)} van {BeschikbareKolommen.Count} velden geselecteerd";
    public string GeselecteerdeRelatiesTekst => $"{BeschikbareRelaties.Count(x => x.IsGeselecteerd)} relatie(s) geselecteerd";
    public string GeselecteerdeEntiteitenTekst => BeschikbareEntiteiten.Count == 0
        ? "Geen records beschikbaar voor deze dataset."
        : BeschikbareEntiteiten.All(x => !x.IsGeselecteerd)
            ? $"Alle {BeschikbareEntiteiten.Count} records worden geëxporteerd."
            : $"{BeschikbareEntiteiten.Count(x => x.IsGeselecteerd)} van {BeschikbareEntiteiten.Count} records geselecteerd.";

    public int TotaalPaginas => Math.Max(1, (int)Math.Ceiling(GetGefilterdEntiteiten().Count() / (double)PaginaGrootte));
    public string PaginaInfo => $"Pagina {HuidigePagina} van {TotaalPaginas}";
    public bool KanNaarVolgendePagina => HuidigePagina < TotaalPaginas;
    public bool KanNaarVorigePagina => HuidigePagina > 1;

    public async Task InitializeAsync()
    {
        ExportFolder = EnsureExportFolder(await GetInitiëleExportMapAsync());
        await LaadMetadataAsync();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private async Task ChooseExportFolderAsync()
    {
        var selectedFolder = await _filePicker.PickFolderAsync("Selecteer exportmap");
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            StatusMessage = "Mapselectie geannuleerd.";
            return;
        }

        ExportFolder = EnsureExportFolder(selectedFolder);
        await _settings.SaveLastExportFolderAsync(ExportFolder);
        StatusMessage = $"Exportmap ingesteld op: {ExportFolder}";
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void SelecteerAlleVelden()
    {
        foreach (var kolom in BeschikbareKolommen)
            kolom.IsGeselecteerd = true;

        foreach (var relatie in BeschikbareRelaties)
        {
            if (!relatie.IsGeselecteerd)
                continue;

            foreach (var kolom in relatie.Kolommen)
                kolom.IsGeselecteerd = true;
        }

        StatusMessage = "Alle zichtbare velden zijn geselecteerd.";
        RaiseSelectionSummary();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void WisVelden()
    {
        foreach (var kolom in BeschikbareKolommen)
            kolom.IsGeselecteerd = false;

        foreach (var relatie in BeschikbareRelaties)
        {
            relatie.IsGeselecteerd = false;
            foreach (var kolom in relatie.Kolommen)
                kolom.IsGeselecteerd = false;
        }

        StatusMessage = "Veld- en relatieselecties gewist.";
        RaiseSelectionSummary();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void SelecteerAlleEntiteiten()
    {
        foreach (var entiteit in BeschikbareEntiteiten)
            entiteit.IsGeselecteerd = true;

        StatusMessage = "Alle records zijn geselecteerd voor gerichte export.";
        RaiseSelectionSummary();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private void WisEntiteiten()
    {
        foreach (var entiteit in BeschikbareEntiteiten)
            entiteit.IsGeselecteerd = false;

        StatusMessage = "Recordfilter gewist. Export gebruikt opnieuw alle records.";
        RaiseSelectionSummary();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private async Task ExporteerConfiguratieAsync()
    {
        if (SelectedDataset is null)
        {
            _toast.Warning("Selecteer eerst een dataset.");
            return;
        }

        var kolomSleutels = BeschikbareKolommen
            .Where(x => x.IsGeselecteerd)
            .Select(x => x.Sleutel)
            .ToList();

        if (kolomSleutels.Count == 0)
        {
            _toast.Warning("Selecteer minstens één hoofdveld.");
            return;
        }

        var relatieAanvragen = BeschikbareRelaties
            .Where(x => x.IsGeselecteerd)
            .Select(x => new ExportRelatieAanvraag
            {
                Sleutel = x.Sleutel,
                KolomSleutels = x.Kolommen.Where(k => k.IsGeselecteerd).Select(k => k.Sleutel).ToList()
            })
            .ToList();

        try
        {
            IsBusy = true;
            ExportFolder = EnsureExportFolder(ExportFolder);
            await _settings.SaveLastExportFolderAsync(ExportFolder);
            StatusMessage = "Export wordt opgebouwd...";

            var result = await _exportService.ExportAsync(new ExportAanvraag
            {
                Dataset = SelectedDataset.Dataset,
                PresetSleutel = SelectedPreset?.Sleutel,
                EntiteitIds = BeschikbareEntiteiten.Where(x => x.IsGeselecteerd).Select(x => x.Id).ToList(),
                KolomSleutels = kolomSleutels,
                Relaties = relatieAanvragen
            }, ExportFolder);

            LastExportPath = result.BestandPad;
            StatusMessage = $"{result.Message} Bestand: {result.BestandPad}";
            _toast.Success("Export voltooid.");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = $"Export mislukt: {message}";
            _toast.Error(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenExportFolder))]
    private void OpenExportFolder()
    {
        try
        {
            _pathOpener.OpenFolder(ExportFolder);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLastExport))]
    private void OpenLastExport()
    {
        try
        {
            _pathOpener.OpenFile(LastExportPath!);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<HomeViewModel>();

    [RelayCommand(CanExecute = nameof(KanNaarVolgendePagina))]
    private void VolgendePagina() => HuidigePagina++;

    [RelayCommand(CanExecute = nameof(KanNaarVorigePagina))]
    private void VorigePagina() => HuidigePagina--;

    private bool CanEditSelection() => !IsBusy;
    private bool CanOpenExportFolder() => !IsBusy && !string.IsNullOrWhiteSpace(ExportFolder);
    private bool CanOpenLastExport() => !IsBusy && HasLastExportPath;

    partial void OnEntiteitZoektermChanged(string value)
    {
        if (HuidigePagina != 1)
            HuidigePagina = 1;
        else
            HerberekeningPaginering();
    }

    partial void OnHuidigePaginaChanged(int value) => HerberekeningPaginering();

    partial void OnSelectedPresetChanged(ExportPresetOptie? value)
    {
        if (_selectionEventsSuppressed || value is null)
            return;

        _ = PasPresetToeAsync(value);
    }

    partial void OnSelectedDatasetChanged(ExportDatasetOptie? value)
    {
        if (_selectionEventsSuppressed || value is null)
            return;

        _ = LaadDatasetAsync(value);
    }

    private async Task LaadMetadataAsync()
    {
        try
        {
            IsBusy = true;
            BeschikbareDatasets = new ObservableCollection<ExportDatasetOptie>(await _exportService.GetBeschikbareDatasetsAsync());
            BeschikbarePresets = new ObservableCollection<ExportPresetOptie>(await _exportService.GetStandaardPresetsAsync());

            var savedPresetSleutel = await _settings.GetLastExportPresetAsync();
            var savedDataset = await _settings.GetLastExportDatasetAsync();
            var opgeslagenPreset = !string.IsNullOrWhiteSpace(savedPresetSleutel)
                ? BeschikbarePresets.FirstOrDefault(x => x.Sleutel == savedPresetSleutel)
                : null;

            if (opgeslagenPreset is not null)
            {
                _selectionEventsSuppressed = true;
                SelectedPreset = opgeslagenPreset;
                SelectedDataset = BeschikbareDatasets.FirstOrDefault(x => x.Dataset == opgeslagenPreset.Dataset);
                _selectionEventsSuppressed = false;
                await LaadConfiguratieAsync(opgeslagenPreset.Dataset, opgeslagenPreset.Sleutel, $"Preset '{opgeslagenPreset.Naam}' geladen.");
                return;
            }

            if (savedDataset is { } dataset && BeschikbareDatasets.FirstOrDefault(x => x.Dataset == dataset) is { } opgeslagenDataset)
            {
                _selectionEventsSuppressed = true;
                SelectedDataset = opgeslagenDataset;
                SelectedPreset = null;
                _selectionEventsSuppressed = false;
                await LaadConfiguratieAsync(opgeslagenDataset.Dataset, null, $"Dataset '{opgeslagenDataset.Naam}' geladen.");
                return;
            }

            var standaardPreset = BeschikbarePresets.FirstOrDefault(x => x.Sleutel == "voorraadoverzicht")
                                 ?? BeschikbarePresets.FirstOrDefault();

            if (standaardPreset is not null)
            {
                _selectionEventsSuppressed = true;
                SelectedPreset = standaardPreset;
                SelectedDataset = BeschikbareDatasets.FirstOrDefault(x => x.Dataset == standaardPreset.Dataset);
                _selectionEventsSuppressed = false;
                await LaadConfiguratieAsync(standaardPreset.Dataset, standaardPreset.Sleutel, $"Preset '{standaardPreset.Naam}' geladen.");
            }
            else if (BeschikbareDatasets.FirstOrDefault() is { } eersteDataset)
            {
                _selectionEventsSuppressed = true;
                SelectedDataset = eersteDataset;
                _selectionEventsSuppressed = false;
                await LaadConfiguratieAsync(eersteDataset.Dataset, null, "Standaardconfiguratie geladen.");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PasPresetToeAsync(ExportPresetOptie preset)
    {
        if (IsBusy)
            return;

        _selectionEventsSuppressed = true;
        SelectedDataset = BeschikbareDatasets.FirstOrDefault(x => x.Dataset == preset.Dataset);
        _selectionEventsSuppressed = false;

        await LaadConfiguratieAsync(preset.Dataset, preset.Sleutel, $"Preset '{preset.Naam}' geladen.");
        await _settings.SaveLastExportSelectionAsync(preset.Dataset, preset.Sleutel);
    }

    private async Task LaadDatasetAsync(ExportDatasetOptie dataset)
    {
        if (IsBusy)
            return;

        _selectionEventsSuppressed = true;
        SelectedPreset = null;
        _selectionEventsSuppressed = false;

        await LaadConfiguratieAsync(dataset.Dataset, null, $"Dataset '{dataset.Naam}' geladen.");
        await _settings.SaveLastExportSelectionAsync(dataset.Dataset, null);
    }

    private async Task LaadConfiguratieAsync(ExcelExportDataset dataset, string? presetSleutel, string status)
    {
        try
        {
            _isLoadingConfiguration = true;
            IsBusy = true;
            var configuratie = await _exportService.MaakConfiguratieAsync(dataset, presetSleutel);

            DetachSelectionEvents();

            BeschikbareEntiteiten = configuratie.Entiteiten;
            BeschikbareKolommen = configuratie.Kolommen;
            BeschikbareRelaties = configuratie.Relaties;
            DatasetBeschrijving = configuratie.Beschrijving;
            _presetHandmatigAangepast = false;
            UpdatePresetBeschrijving();

            AttachSelectionEvents();
            RaiseSelectionSummary();

            EntiteitZoekterm = string.Empty;
            HuidigePagina = 1;
            HerberekeningPaginering();

            StatusMessage = status;
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = $"Configuratie laden mislukt: {message}";
            _toast.Error(StatusMessage);
        }
        finally
        {
            _isLoadingConfiguration = false;
            IsBusy = false;
        }
    }

    private void AttachSelectionEvents()
    {
        foreach (var entiteit in BeschikbareEntiteiten)
            entiteit.PropertyChanged += OnSelectionChanged;

        foreach (var kolom in BeschikbareKolommen)
            kolom.PropertyChanged += OnSelectionChanged;

        foreach (var relatie in BeschikbareRelaties)
        {
            relatie.PropertyChanged += OnSelectionChanged;
            foreach (var kolom in relatie.Kolommen)
                kolom.PropertyChanged += OnSelectionChanged;
        }
    }

    private void DetachSelectionEvents()
    {
        foreach (var entiteit in BeschikbareEntiteiten)
            entiteit.PropertyChanged -= OnSelectionChanged;

        foreach (var kolom in BeschikbareKolommen)
            kolom.PropertyChanged -= OnSelectionChanged;

        foreach (var relatie in BeschikbareRelaties)
        {
            relatie.PropertyChanged -= OnSelectionChanged;
            foreach (var kolom in relatie.Kolommen)
                kolom.PropertyChanged -= OnSelectionChanged;
        }
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExportKolomOptie.IsGeselecteerd)
            || e.PropertyName == nameof(ExportRelatieOptie.IsGeselecteerd)
            || e.PropertyName == nameof(ExportEntiteitOptie.IsGeselecteerd))
        {
            if (!_isLoadingConfiguration && SelectedPreset is not null)
            {
                _presetHandmatigAangepast = true;
                UpdatePresetBeschrijving();
            }

            RaiseSelectionSummary();
        }
    }

    private void RaiseSelectionSummary()
    {
        OnPropertyChanged(nameof(GeselecteerdeEntiteitenTekst));
        OnPropertyChanged(nameof(GeselecteerdeVeldenTekst));
        OnPropertyChanged(nameof(GeselecteerdeRelatiesTekst));
        OnPropertyChanged(nameof(HeeftRelaties));
        OnPropertyChanged(nameof(GeenRelaties));
        OnPropertyChanged(nameof(HeeftEntiteiten));
        OnPropertyChanged(nameof(GeenEntiteiten));
    }

    private void UpdatePresetBeschrijving()
    {
        GeselecteerdePresetBeschrijving = SelectedPreset switch
        {
            null => "Geen preset geselecteerd. Je werkt met een aangepaste selectie.",
            _ when _presetHandmatigAangepast => $"Preset '{SelectedPreset.Naam}' geladen. De selectie werd daarna handmatig aangepast.",
            _ => SelectedPreset.Beschrijving
        };
    }

    private async Task<string> GetInitiëleExportMapAsync()
    {
        var savedFolder = await _settings.GetLastExportFolderAsync();
        return string.IsNullOrWhiteSpace(savedFolder)
            ? ExportFolder
            : savedFolder;
    }

    private static string GetDefaultExportFolder()
        => Path.Combine(AppContext.BaseDirectory, "exports");

    private static string EnsureExportFolder(string folder)
    {
        var normalized = Path.GetFullPath(folder);
        Directory.CreateDirectory(normalized);
        return normalized;
    }

    private IEnumerable<ExportEntiteitOptie> GetGefilterdEntiteiten()
    {
        if (string.IsNullOrWhiteSpace(EntiteitZoekterm))
            return BeschikbareEntiteiten;

        return BeschikbareEntiteiten.Where(e =>
            e.Label.Contains(EntiteitZoekterm, StringComparison.OrdinalIgnoreCase) ||
            e.Beschrijving.Contains(EntiteitZoekterm, StringComparison.OrdinalIgnoreCase));
    }

    private void HerberekeningPaginering()
    {
        var gefilterd = GetGefilterdEntiteiten()
            .Skip((HuidigePagina - 1) * PaginaGrootte)
            .Take(PaginaGrootte)
            .ToList();

        GepagineerdeEntiteiten = new ObservableCollection<ExportEntiteitOptie>(gefilterd);

        OnPropertyChanged(nameof(TotaalPaginas));
        OnPropertyChanged(nameof(PaginaInfo));
        OnPropertyChanged(nameof(KanNaarVolgendePagina));
        OnPropertyChanged(nameof(KanNaarVorigePagina));
        VolgendePaginaCommand.NotifyCanExecuteChanged();
        VorigePaginaCommand.NotifyCanExecuteChanged();
    }
}
