using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Windows.Input;

namespace QuadroApp.Views.Controls;

/// <summary>
/// Herbruikbare kopbalk voor de Quadro-schermen: donkere balk met logo,
/// "QUADRO" + een view-titel, en links een optionele Terug-knop.
/// Gebruik: &lt;controls:QuadroHeader Title="ARCHIEF" BackCommand="{Binding GaTerugCommand}"/&gt;
/// Zet ShowBackButton="False" op schermen zonder terugnavigatie (kalender, weekwerklijst).
/// </summary>
public partial class QuadroHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<QuadroHeader, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowBackButtonProperty =
        AvaloniaProperty.Register<QuadroHeader, bool>(nameof(ShowBackButton), defaultValue: true);

    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<QuadroHeader, ICommand?>(nameof(BackCommand));

    public QuadroHeader() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowBackButton
    {
        get => GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public ICommand? BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }
}
