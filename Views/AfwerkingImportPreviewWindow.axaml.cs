// Candidate for removal – requires runtime verification
using Avalonia;
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace QuadroApp.Views;

[Obsolete("Not used in current startup flow. Remove after runtime verification.")]
public partial class AfwerkingImportPreviewWindow : Window
{
    public AfwerkingImportPreviewWindow()
    {
        InitializeComponent();
    }
}