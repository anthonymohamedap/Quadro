using QuadroApp.Service.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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

        public Task GoWerkbonAsync() => _nav.NavigateToAsync<WerkBonLijstViewModel>();
        public Task GoHomeAsync() => _nav.NavigateToAsync<HomeViewModel>();
        public Task GoLijstenAsync() => _nav.NavigateToAsync<LijstenViewModel>();
        public Task GoKlantenAsync() => _nav.NavigateToAsync<KlantenViewModel>();
        public Task GoOffertesAsync() => _nav.NavigateToAsync<OffertesLijstViewModel>();
        public Task GoAfwerkingAsync() => _nav.NavigateToAsync<AfwerkingenViewModel>();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}