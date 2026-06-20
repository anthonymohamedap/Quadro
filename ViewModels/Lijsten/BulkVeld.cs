using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace QuadroApp.ViewModels;

/// <summary>
/// Niet-generieke interface zodat de VM over alle bulk-velden kan itereren
/// zonder generieke type-parameters.
/// </summary>
public interface IBulkVeld
{
    string Label { get; }
    string ValidationFieldName { get; }
    bool Bijwerken { get; }
    bool IsGeldig { get; }
    void Reset();
    event Action? Changed;
}

/// <summary>
/// Bundelt de "bijwerken"-toggle, de nieuwe waarde en het optionele "leegmaken"-vlag
/// voor één bulk-bewerkingsveld. Implementeert <see cref="IBulkVeld"/> zodat de VM
/// over alle velden kan itereren voor CanExecute, reset en validatie.
/// </summary>
public partial class BulkVeld<T> : ObservableObject, IBulkVeld
{
    private readonly Func<T, bool>? _isWaardeGeldig;
    private readonly T _defaultWaarde;

    public string Label { get; }
    public string ValidationFieldName { get; }

    [ObservableProperty] private bool bijwerken;
    [ObservableProperty] private T waarde = default!;
    [ObservableProperty] private bool leegmaken; // alleen voor nullable velden met "leegmaken"-optie

    public BulkVeld(
        string label,
        string validationFieldName = "",
        T defaultWaarde = default!,
        Func<T, bool>? isWaardeGeldig = null)
    {
        Label = label;
        ValidationFieldName = validationFieldName;
        _defaultWaarde = defaultWaarde;
        _isWaardeGeldig = isWaardeGeldig;
        waarde = defaultWaarde; // stel backing field in zonder PropertyChanged-notificatie
    }

    /// <summary>
    /// <c>true</c> als het veld niet geselecteerd is, als <see cref="Leegmaken"/> actief is,
    /// of als de ingevulde waarde geldig is.
    /// </summary>
    public bool IsGeldig
    {
        get
        {
            if (!Bijwerken) return true;
            if (Leegmaken) return true;
            if (_isWaardeGeldig is not null) return _isWaardeGeldig(Waarde);
            return DefaultValideer(Waarde);
        }
    }

    /// <summary>Zet alle waarden terug naar de standaardtoestand.</summary>
    public void Reset()
    {
        Bijwerken = false;
        Waarde = _defaultWaarde;
        Leegmaken = false;
    }

    /// <summary>Wordt gevuurd wanneer Bijwerken, Waarde of Leegmaken verandert.</summary>
    public event Action? Changed;

    partial void OnBijwerkenChanged(bool value) => Changed?.Invoke();
    partial void OnWaardeChanged(T value) => Changed?.Invoke();
    partial void OnLeegmakenChanged(bool value) => Changed?.Invoke();

    private static bool DefaultValideer(T waarde)
    {
        if (waarde is null) return false;
        if (waarde is string s) return !string.IsNullOrWhiteSpace(s);
        return true;
    }
}
