using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace QuadroApp.ViewModels;

/// <summary>
/// Beheert de variant-collectie voor de geselecteerde afwerkingsoptie.
/// Wordt aangestuurd vanuit <see cref="AfwerkingenViewModel"/> via het
/// <c>VariantBeheer</c>-property.
/// </summary>
public partial class AfwerkingVariantBeheer : ObservableObject
{
    private readonly ObservableCollection<AfwerkingsVariantViewModel> _optieVarianten = new();
    public ObservableCollection<AfwerkingsVariantViewModel> OptieVarianten => _optieVarianten;

    private readonly List<int> _pendingDeleteVariantIds = new();
    public IReadOnlyList<int> PendingDeleteVariantIds => _pendingDeleteVariantIds;

    public bool HeeftGeenVarianten => !_optieVarianten.Any();

    private readonly Action _notifyHasChanges;
    private AfwerkingsOptie? _huidigOptie;

    public AfwerkingVariantBeheer(Action notifyHasChanges)
    {
        _notifyHasChanges = notifyHasChanges;
        _optieVarianten.CollectionChanged += (_, _) =>
            OnPropertyChanged(nameof(HeeftGeenVarianten));
    }

    // ───────── Laden ─────────

    /// <summary>Laadt de varianten voor de gegeven optie (of leegt de collectie als null).</summary>
    public void LaadVoorOptie(AfwerkingsOptie? optie)
    {
        _huidigOptie = optie;
        _optieVarianten.Clear();
        _pendingDeleteVariantIds.Clear();
        AddVariantCommand.NotifyCanExecuteChanged();

        if (optie is null) return;

        foreach (var v in (optie.Varianten ?? Enumerable.Empty<AfwerkingsVariant>())
                     .OrderByDescending(x => x.IsStandaard)
                     .ThenBy(x => x.Beschrijving))
        {
            _optieVarianten.Add(new AfwerkingsVariantViewModel(v, SetStandaard, DeleteVariant));
        }
    }

    /// <summary>Verwijdert alle pending-delete-ids na een succesvolle opslag.</summary>
    public void ClearPendingDeletes() => _pendingDeleteVariantIds.Clear();

    // ───────── Variant toevoegen ─────────

    [RelayCommand(CanExecute = nameof(HeeftOptieGeselecteerd))]
    private void AddVariant()
    {
        if (_huidigOptie is null) return;

        var variant = new AfwerkingsVariant
        {
            AfwerkingsOptieId = _huidigOptie.Id,
            Beschrijving = "",
            IsActief = true,
            IsStandaard = !_optieVarianten.Any() // eerste variant is automatisch standaard
        };

        _optieVarianten.Add(new AfwerkingsVariantViewModel(variant, SetStandaard, DeleteVariant));
        _notifyHasChanges();
    }

    private bool HeeftOptieGeselecteerd() => _huidigOptie is not null;

    // ───────── Variant verwijderen ─────────

    private void DeleteVariant(AfwerkingsVariantViewModel vm)
    {
        // Persist delete on next Save.
        if (vm.Id > 0)
            _pendingDeleteVariantIds.Add(vm.Id);

        _optieVarianten.Remove(vm);

        // Promote first remaining active variant to standaard if none is set.
        if (!_optieVarianten.Any(v => v.IsStandaard))
        {
            var first = _optieVarianten.FirstOrDefault(v => v.IsActief);
            if (first is not null)
                first.IsStandaard = true;
        }

        _notifyHasChanges();
    }

    // ───────── Standaard instellen ─────────

    /// <summary>Zorgt ervoor dat slechts één variant IsStandaard = true draagt.</summary>
    private void SetStandaard(AfwerkingsVariantViewModel chosen)
    {
        foreach (var v in _optieVarianten.Where(v => v != chosen))
            v.IsStandaard = false;
        _notifyHasChanges();
    }
}
