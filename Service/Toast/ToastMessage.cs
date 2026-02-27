namespace QuadroApp.Model.Toast
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using QuadroApp.Service.Toast;

    public partial class ToastMessage : ObservableObject
    {
        public ToastMessage(string content, ToastType type)
        {
            Content = content;
            Type = type;
        }

        public string Content { get; }
        public ToastType Type { get; }

        [ObservableProperty]
        private bool isVisible = true;
    }
}
