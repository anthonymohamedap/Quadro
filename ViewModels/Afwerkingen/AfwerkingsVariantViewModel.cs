using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using System;

namespace QuadroApp.ViewModels;

/// <summary>
/// Wrapper ViewModel voor een AfwerkingsVariant; biedt INPC en inline-editing
/// in de AfwerkingenView zonder de model-klasse aan te passen.
/// </summary>
public partial class AfwerkingsVariantViewModel : ObservableObject
{
    private readonly AfwerkingsVariant _source;
    private readonly Action<AfwerkingsVariantViewModel> _onSetStandaard;

    // ── Identity ─────────────────────────────────────────────────────────────
    public int Id              => _source.Id;
    public int AfwerkingsOptieId => _source.AfwerkingsOptieId;
    public bool IsNieuw        => _source.Id == 0;

    // ── Editable properties ───────────────────────────────────────────────────
    [ObservableProperty] private string  beschrijving = string.Empty;
    [ObservableProperty] private string? kleur;
    [ObservableProperty] private string? variantCode;
    [ObservableProperty] private bool    isStandaard;
    [ObservableProperty] private bool    isActief = true;

    // ── Commands ──────────────────────────────────────────────────────────────
    public IRelayCommand DeleteCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public AfwerkingsVariantViewModel(
        AfwerkingsVariant variant,
        Action<AfwerkingsVariantViewModel> onSetStandaard,
        Action<AfwerkingsVariantViewModel> onDelete)
    {
        _source           = variant ?? throw new ArgumentNullException(nameof(variant));
        _onSetStandaard   = onSetStandaard;

        beschrijving  = variant.Beschrijving;
        kleur         = variant.Kleur;
        variantCode   = variant.VariantCode;
        isStandaard   = variant.IsStandaard;
        isActief      = variant.IsActief;

        DeleteCommand = new RelayCommand(() => onDelete(this));
    }

    // ── Standaard-exclusive logic ─────────────────────────────────────────────
    partial void OnIsStandaardChanged(bool value)
    {
        // When this item becomes the standaard, clear all others via callback.
        if (value)
            _onSetStandaard(this);
    }

    // ── Map back to DB entity ─────────────────────────────────────────────────
    public AfwerkingsVariant ToVariant()
    {
        _source.Beschrijving = Beschrijving?.Trim() ?? string.Empty;
        _source.Kleur        = string.IsNullOrWhiteSpace(Kleur) ? null : Kleur.Trim();
        _source.VariantCode  = string.IsNullOrWhiteSpace(VariantCode) ? null : VariantCode.Trim();
        _source.IsStandaard  = IsStandaard;
        _source.IsActief     = IsActief;
        return _source;
    }
}
