using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Verkoop;
using System;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>
/// US-94 — afdrukken in het dossier. De werkbon (voor het atelier, standaard zonder prijzen) en de
/// bestelbon worden als PDF bewaard in Documenten › QuadroApp en geopend in het systeemvenster,
/// waar je afdrukt (beslist 02/11: printvenster van het systeem).
/// </summary>
public partial class VerkoopDossierViewModel
{
    /// <summary>"🖨 Werkbon afdrukken" vanaf stap 2 (zodra er een werkbon is).</summary>
    public bool KanWerkbonAfdrukken => _offerte?.WerkBon is not null && Info is { IsGeannuleerd: false } && BekekenStap >= VerkoopStap.Werkbon;

    /// <summary>Pad van het laatst gemaakte document (voor de melding en de tests).</summary>
    public string? LaatsteDocument { get; private set; }

    [RelayCommand]
    private Task WerkbonAfdrukkenAsync() => MaakWerkbonPdfAsync(metPrijzen: false);

    [RelayCommand]
    private Task WerkbonAfdrukkenMetPrijzenAsync() => MaakWerkbonPdfAsync(metPrijzen: true);

    private async Task MaakWerkbonPdfAsync(bool metPrijzen)
    {
        if (_offerte?.WerkBon is not { } wb) return;
        try
        {
            var data = await PdfWerkBonExporter.LeesAsync(_factory, wb.Id);
            var pad = new PdfWerkBonExporter().Export(data, metPrijzen);
            LaatsteDocument = pad;
            (_services?.GetService(typeof(IPathOpener)) as IPathOpener)?.OpenFile(pad);
            _toast.Success($"Werkbon {data.Nummer} staat klaar om af te drukken (Documenten › QuadroApp › Werkbonnen).");
        }
        catch (Exception ex)
        {
            _toast.Error($"Afdrukken mislukt: {ex.GetBaseException().Message}");
        }
    }

    private void WerkAfdrukkenBij() => OnPropertyChanged(nameof(KanWerkbonAfdrukken));
}
