using QuadroApp.Service.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuadroApp.ViewModels
{


    public sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _nav;
        private readonly IToastService _toast;

        public IToastService Toast => _toast;   // 👈 BELANGRIJK

        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            private set { _currentViewModel = value; OnPropertyChanged(); }
        }

        public MainWindowViewModel(
            INavigationService nav,
            IToastService toast)
        {
            _nav = nav;
            _toast = toast;

            _nav.CurrentViewModelChanged += vm => CurrentViewModel = vm;

            _ = _nav.NavigateToAsync<HomeViewModel>();
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}