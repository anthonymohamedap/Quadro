using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace QuadroApp.Service.Model;

public sealed partial class ExportKolomOptie : ObservableObject
{
    public string Sleutel { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Groep { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isGeselecteerd;
}

public sealed partial class ExportRelatieOptie : ObservableObject
{
    public string Sleutel { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Beschrijving { get; init; } = string.Empty;
    public string WerkbladNaam { get; init; } = string.Empty;
    public ObservableCollection<ExportKolomOptie> Kolommen { get; init; } = new();

    [ObservableProperty]
    private bool isGeselecteerd;
}

public sealed partial class ExportEntiteitOptie : ObservableObject
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Beschrijving { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isGeselecteerd;
}

public sealed class ExportPresetOptie
{
    public string Sleutel { get; init; } = string.Empty;
    public string Naam { get; init; } = string.Empty;
    public string Beschrijving { get; init; } = string.Empty;
    public ExcelExportDataset Dataset { get; init; }
}

public sealed class ExportDatasetOptie
{
    public ExcelExportDataset Dataset { get; init; }
    public string Naam { get; init; } = string.Empty;
    public string Beschrijving { get; init; } = string.Empty;
}

public sealed class ExportConfiguratie
{
    public ExcelExportDataset Dataset { get; init; }
    public string Titel { get; init; } = string.Empty;
    public string Beschrijving { get; init; } = string.Empty;
    public ObservableCollection<ExportEntiteitOptie> Entiteiten { get; init; } = new();
    public ObservableCollection<ExportKolomOptie> Kolommen { get; init; } = new();
    public ObservableCollection<ExportRelatieOptie> Relaties { get; init; } = new();
}

public sealed class ExportRelatieAanvraag
{
    public string Sleutel { get; init; } = string.Empty;
    public IReadOnlyList<string> KolomSleutels { get; init; } = [];
}

public sealed class ExportAanvraag
{
    public ExcelExportDataset Dataset { get; init; }
    public string? PresetSleutel { get; init; }
    public IReadOnlyList<int> EntiteitIds { get; init; } = [];
    public IReadOnlyList<string> KolomSleutels { get; init; } = [];
    public IReadOnlyList<ExportRelatieAanvraag> Relaties { get; init; } = [];
}
