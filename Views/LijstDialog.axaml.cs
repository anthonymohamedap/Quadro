using Avalonia.Controls;
using Avalonia.Interactivity;
using QuadroApp.Model.DB;

namespace QuadroApp.Views;

public partial class LijstDialog : Window
{
    private readonly TypeLijst _original;
    private readonly TypeLijst _editCopy;

    public LijstDialog()
    {
        InitializeComponent();
        _original = new TypeLijst();
        _editCopy = new TypeLijst();
        DataContext = _editCopy;
    }

    public LijstDialog(TypeLijst lijst) : this()
    {
        _original = lijst;

        _editCopy.Id = lijst.Id;
        _editCopy.Artikelnummer = lijst.Artikelnummer;
        _editCopy.Opmerking = lijst.Opmerking;
        _editCopy.BreedteCm = lijst.BreedteCm;
        _editCopy.PrijsPerMeter = lijst.PrijsPerMeter;
        _editCopy.WinstMargeFactor = lijst.WinstMargeFactor;
        _editCopy.AfvalPercentage = lijst.AfvalPercentage;
        _editCopy.VasteKost = lijst.VasteKost;
        _editCopy.WerkMinuten = lijst.WerkMinuten;
        _editCopy.LeverancierId = lijst.LeverancierId;
        _editCopy.LaatsteUpdate = lijst.LaatsteUpdate;

        // Als je UI dit gebruikt:
        _editCopy.AlleLeveranciers = lijst.AlleLeveranciers;
        _editCopy.Leverancier = lijst.Leverancier;

        DataContext = _editCopy;
    }

    private void Annuleer_Click(object? sender, RoutedEventArgs e)
        => Close(null);

    private void Opslaan_Click(object? sender, RoutedEventArgs e)
        => Close(_editCopy);
}
