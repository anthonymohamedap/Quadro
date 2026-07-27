using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>Eén auditregel voor weergave (Tijdstip staat in UTC → converter in de view).</summary>
public sealed record AuditRegelWeergave(
    DateTime Tijdstip,
    string Gebruiker,
    string EntiteitType,
    string EntiteitId,
    string Actie,
    string Wijzigingen);

/// <summary>US-40 — audit-leesscherm (admin-only): filteren + pagineren, alleen-lezen.</summary>
public partial class AuditLogViewModel : ObservableObject
{
    private const int PaginaGrootte = 100;
    private readonly IAuditService _audit;

    public ObservableCollection<AuditRegelWeergave> Records { get; } = new();
    public ObservableCollection<string> GebruikerOpties { get; } = new();
    public ObservableCollection<string> TypeOpties { get; } = new();
    public ObservableCollection<string> ActieOpties { get; } = new();

    [ObservableProperty] private string? gekozenGebruiker;
    [ObservableProperty] private string? gekozenType;
    [ObservableProperty] private string? gekozenActie;
    [ObservableProperty] private DateTimeOffset? vanafDatum;
    [ObservableProperty] private DateTimeOffset? totDatum;

    [ObservableProperty] private int huidigePagina = 1;
    [ObservableProperty] private int totaalAantal;
    [ObservableProperty] private bool isBezig;

    public bool KanVorige => HuidigePagina > 1 && !IsBezig;
    public bool KanVolgende => HuidigePagina * PaginaGrootte < TotaalAantal && !IsBezig;

    public string PaginaInfo => TotaalAantal == 0
        ? "Geen records"
        : $"{((HuidigePagina - 1) * PaginaGrootte) + 1}–{Math.Min(HuidigePagina * PaginaGrootte, TotaalAantal)} van {TotaalAantal}";

    public AuditLogViewModel(IAuditService audit) => _audit = audit;

    public async Task InitializeAsync()
    {
        var (gebruikers, types, acties) = await _audit.GetFilterOptiesAsync();
        Vul(GebruikerOpties, gebruikers);
        Vul(TypeOpties, types);
        Vul(ActieOpties, acties);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task ZoekAsync()
    {
        HuidigePagina = 1;
        await LaadAsync();
    }

    [RelayCommand]
    private async Task WisFilterAsync()
    {
        GekozenGebruiker = null;
        GekozenType = null;
        GekozenActie = null;
        VanafDatum = null;
        TotDatum = null;
        HuidigePagina = 1;
        await LaadAsync();
    }

    [RelayCommand(CanExecute = nameof(KanVorige))]
    private async Task VorigeAsync()
    {
        HuidigePagina--;
        await LaadAsync();
    }

    [RelayCommand(CanExecute = nameof(KanVolgende))]
    private async Task VolgendeAsync()
    {
        HuidigePagina++;
        await LaadAsync();
    }

    private async Task LaadAsync()
    {
        if (IsBezig) return;
        IsBezig = true;
        NotifyPaginering();
        try
        {
            var filter = new AuditFilter(
                string.IsNullOrWhiteSpace(GekozenGebruiker) ? null : GekozenGebruiker,
                string.IsNullOrWhiteSpace(GekozenType) ? null : GekozenType,
                string.IsNullOrWhiteSpace(GekozenActie) ? null : GekozenActie,
                VanafDatum?.LocalDateTime,
                TotDatum?.LocalDateTime);

            var pagina = await _audit.ZoekAsync(filter, (HuidigePagina - 1) * PaginaGrootte, PaginaGrootte);
            TotaalAantal = pagina.TotaalAantal;

            Records.Clear();
            foreach (var r in pagina.Records)
                Records.Add(new AuditRegelWeergave(
                    r.Tijdstip, r.Gebruiker, r.EntiteitType, r.EntiteitId, r.Actie, FormatWijzigingen(r)));
        }
        finally
        {
            IsBezig = false;
            NotifyPaginering();
        }
    }

    private void NotifyPaginering()
    {
        OnPropertyChanged(nameof(KanVorige));
        OnPropertyChanged(nameof(KanVolgende));
        OnPropertyChanged(nameof(PaginaInfo));
        VorigeCommand.NotifyCanExecuteChanged();
        VolgendeCommand.NotifyCanExecuteChanged();
    }

    private static string FormatWijzigingen(AuditLog log)
    {
        if (log.Actie == "Verwijderd") return "(record verwijderd)";
        var regels = AuditWijzigingParser.Beschrijf(log.Wijzigingen);
        return regels.Count == 0 ? "—" : string.Join("  ·  ", regels);
    }

    private static void Vul(ObservableCollection<string> doel, System.Collections.Generic.IReadOnlyList<string> bron)
    {
        doel.Clear();
        foreach (var x in bron) doel.Add(x);
    }
}
