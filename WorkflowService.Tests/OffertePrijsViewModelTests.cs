using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Toast;
using QuadroApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-54 — de gedebouncte, stille herberekening (showFeedback: false) mag niet stil overslaan
/// omdat de offerte als geheel (nog) niet volledig valideert (bv. tijdens het invullen van een
/// 2e regel). Zie OffertePrijsViewModel.BerekenAsync.
/// </summary>
public class OffertePrijsViewModelTests
{
    [Fact]
    public async Task BerekenSilentAsync_recalculates_even_when_full_offerte_validation_fails()
    {
        var pricing = new RecordingPricingService();
        Offerte? applied = null;

        var vm = new OffertePrijsViewModel(
            pricing,
            new NoopDialogService(),
            new TestToastService(),
            runFullValidation: _ => Task.FromResult(false), // simuleert een (nog) onvolledige 2e regel
            refreshLijstPrijzen: () => Task.CompletedTask,
            buildSnapshot: () => new Offerte { Regels = new List<OfferteRegel> { new() { TotaalExcl = 12.34m } } },
            applySnapshot: snap => applied = snap);

        await vm.BerekenSilentAsync();

        Assert.True(pricing.WasCalled);
        Assert.NotNull(applied);
    }

    [Fact]
    public async Task BerekenCommand_blijft_geblokkeerd_door_volledige_validatie_bij_expliciete_actie()
    {
        // showFeedback: true (de expliciete "Bereken"-knop) moet WEL blijven blokkeren
        // op een falende volledige-offerte-validatie, zodat de gebruiker een duidelijke
        // foutmelding krijgt in plaats van een halfslachtige berekening.
        var pricing = new RecordingPricingService();

        var vm = new OffertePrijsViewModel(
            pricing,
            new NoopDialogService(),
            new TestToastService(),
            runFullValidation: _ => Task.FromResult(false),
            refreshLijstPrijzen: () => Task.CompletedTask,
            buildSnapshot: () => new Offerte { Regels = new List<OfferteRegel>() },
            applySnapshot: _ => { });

        await vm.BerekenCommand.ExecuteAsync(null);

        Assert.False(pricing.WasCalled);
    }

    private sealed class RecordingPricingService : IPricingService
    {
        public bool WasCalled { get; private set; }
        public Task BerekenAsync(int offerteId) => Task.CompletedTask;
        public Task BerekenAsync(Offerte offerte)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopDialogService : IDialogService
    {
        public Task<bool> ShowImportPreviewAsync(
            ObservableCollection<TypeLijstPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues) => Task.FromResult(false);

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);

        public Task<bool> ShowKlantImportPreviewAsync(
            ObservableCollection<KlantPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues) => Task.FromResult(false);

        public Task<bool> ShowAfwerkingImportPreviewAsync(
            ObservableCollection<AfwerkingsOptiePreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues) => Task.FromResult(false);

        public Task<bool> ShowUnifiedImportPreviewAsync(IImportPreviewDefinition definition) => Task.FromResult(false);
    }

    private sealed class TestToastService : IToastService
    {
        public ReadOnlyObservableCollection<ToastMessage> Messages { get; } =
            new(new ObservableCollection<ToastMessage>());

        public void Show(string message, ToastType type, int durationMs = 3000) { }
        public void Success(string message) { }
        public void Error(string message) { }
        public void Warning(string message) { }
        public void Info(string message) { }
        public void Info(string message, string actionLabel, Action onAction, int durationMs = 30_000) { }
    }
}
