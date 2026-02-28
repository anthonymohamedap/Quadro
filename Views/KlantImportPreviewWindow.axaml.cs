// Candidate for removal – requires runtime verification
using Avalonia.Controls;
using System;

namespace QuadroApp.Views;

[Obsolete("Not used in current startup flow. Remove after runtime verification.")]
public partial class KlantImportPreviewWindow : Window
{
    public KlantImportPreviewWindow()
    {
        InitializeComponent();
    }


}