// Candidate for removal – requires runtime verification
﻿using QuadroApp.Model.Import;
using QuadroApp.Service.Import.Enterprise;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IDialogService
    {
        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        Task<bool> ShowImportPreviewAsync(
            ObservableCollection<TypeLijstPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues);

        Task ShowErrorAsync(string title, string message);
        Task<bool> ConfirmAsync(string title, string message);
        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        Task<bool> ShowKlantImportPreviewAsync(
            ObservableCollection<KlantPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues);

        [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
        Task<bool> ShowAfwerkingImportPreviewAsync(
            ObservableCollection<AfwerkingsOptiePreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues);

        Task<bool> ShowUnifiedImportPreviewAsync(IImportPreviewDefinition definition);
    }
}