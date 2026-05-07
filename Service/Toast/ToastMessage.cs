using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace QuadroApp.Service.Toast
{
    public partial class ToastMessage : ObservableObject
    {
        public ToastMessage(string content, ToastType type)
        {
            Content = content;
            Type = type;
        }

        public string Content { get; }

        /// <summary>Alias used by AXAML DataTemplates that bind to {Binding Message}.</summary>
        public string Message => Content;

        /// <summary>Optional label for an action button shown below the message.</summary>
        public string? ActionLabel { get; init; }

        /// <summary>Command invoked when the action button is clicked.</summary>
        public ICommand? ActionCommand { get; init; }

        /// <summary>True when this toast carries an action button.</summary>
        public bool HasAction => ActionLabel is not null && ActionCommand is not null;

        public ToastType Type { get; }

        /// <summary>Type label shown as the card title.</summary>
        public string Title => Type switch
        {
            ToastType.Success => "Success",
            ToastType.Error   => "Error",
            ToastType.Warning => "Warning",
            _                 => "Information"
        };

        /// <summary>Accent brush used for the coloured top border (Growl-style).</summary>
        public IBrush AccentBrush => Type switch
        {
            ToastType.Success => new SolidColorBrush(Color.Parse("#52c41a")),
            ToastType.Error   => new SolidColorBrush(Color.Parse("#ff4d4f")),
            ToastType.Warning => new SolidColorBrush(Color.Parse("#faad14")),
            _                 => new SolidColorBrush(Color.Parse("#1677ff"))
        };

        /// <summary>Background brush derived from <see cref="Type"/> for AXAML overlays.</summary>
        public IBrush BackgroundBrush => Type switch
        {
            ToastType.Success => new SolidColorBrush(Color.Parse("#52c41a")),
            ToastType.Error => new SolidColorBrush(Color.Parse("#ff4d4f")),
            ToastType.Warning => new SolidColorBrush(Color.Parse("#faad14")),
            _ => new SolidColorBrush(Color.Parse("#1677ff"))
        };

        [ObservableProperty]
        private bool isVisible = true;
    }
}
