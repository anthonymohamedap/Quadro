// Candidate for removal – requires runtime verification
﻿using Avalonia.Controls;
using Avalonia.Threading;
using System;
using Microsoft.Extensions.Logging;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using QuadroApp.ViewModels;
using QuadroApp.Views;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public sealed class DialogService : IDialogService
    {
        private readonly IWindowProvider _windowProvider;
        private readonly IFilePickerService _filePicker;
        private readonly IToastService _toast;
        private readonly ILoggerFactory _loggerFactory;

        public DialogService(
            IWindowProvider windowProvider,
            IFilePickerService filePicker,
            IToastService toast,
            ILoggerFactory loggerFactory)
        {
            _windowProvider = windowProvider;
            _filePicker = filePicker;
            _toast = toast;
            _loggerFactory = loggerFactory;
        }

        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        public async Task<bool> ShowImportPreviewAsync(
            ObservableCollection<TypeLijstPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null)
            {
                return false;
            }

            var result = false;
            var window = new ImportPreviewWindow();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            window.DataContext = new ImportPreviewViewModel(previewRows, issues, Close);
            await window.ShowDialog<bool>(owner);
            return result;
        }

        public async Task<bool> ShowUnifiedImportPreviewAsync(IImportPreviewDefinition definition)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null)
            {
                return false;
            }

            var result = false;
            var window = new ImportPreviewView();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close();
            }

            window.DataContext = new ImportPreviewViewModel(
                definition,
                _filePicker,
                _toast,
                _loggerFactory.CreateLogger<ImportPreviewViewModel>(),
                Close);

            await window.ShowDialog(owner);
            return result;
        }

        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        public async Task<bool> ShowKlantImportPreviewAsync(
            ObservableCollection<KlantPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null)
                return false;

            bool result = false;

            var window = new KlantImportPreviewWindow();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            window.DataContext = new KlantExcelPreviewViewModel(
                previewRows,
                issues,
                Close
            );

            await window.ShowDialog<bool>(owner);
            return result;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = _windowProvider.GetMainWindow();
                if (owner == null) return;

                var window = new Window
                {
                    Title = title,
                    Width = 520,
                    Height = 220,
                    Content = new TextBlock
                    {
                        Text = message,
                        Margin = new Avalonia.Thickness(16),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };

                await window.ShowDialog(owner);
            });
        }

        public async Task<bool> ConfirmAsync(string title, string message)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null) return false;

            var tcs = new TaskCompletionSource<bool>();

            var yes = new Button { Content = "Ja", MinWidth = 90 };
            var no = new Button { Content = "Nee", MinWidth = 90 };

            var window = new Window
            {
                Title = title,
                Width = 460,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Children = { no, yes }
                        }
                    }
                }
            };

            yes.Click += (_, _) => { tcs.TrySetResult(true); window.Close(); };
            no.Click += (_, _) => { tcs.TrySetResult(false); window.Close(); };
            window.Closed += (_, _) => tcs.TrySetResult(false);

            await window.ShowDialog(owner);
            return await tcs.Task;
        }

        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        public async Task<bool> ShowAfwerkingImportPreviewAsync(
            ObservableCollection<AfwerkingsOptiePreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null) return false;

            bool result = false;

            var window = new AfwerkingImportPreviewWindow();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            window.DataContext = new AfwerkingExcelPreviewViewModel(
                previewRows,
                Close
            );

            await window.ShowDialog<bool>(owner);
            return result;
        }
    }
}