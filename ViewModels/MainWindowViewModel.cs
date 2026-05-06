using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Interfaces;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace QuadroApp.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _nav;
        private readonly IToastService _toast;

        public IToastService Toast => _toast;
        public IAsyncRelayCommand GoLeveranciersCommand { get; }

        /// <summary>Displays the running assembly version, e.g. "v1.0.4".</summary>
        public static string AppVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly()
                                .GetName().Version;
                return v is null ? "v?" : $"v{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        private object? _currentViewModel;
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            private set { _currentViewModel = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True while the database is being initialised in the background.
        /// Bind a loading overlay to this so the user sees the app start instantly
        /// but can't interact until the DB is ready.
        /// </summary>
        private bool _isInitializing = true;
        public bool IsInitializing
        {
            get => _isInitializing;
            internal set { _isInitializing = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Set when startup DB initialisation fails. Show this in the UI
        /// so the user knows what went wrong instead of seeing a frozen window.
        /// </summary>
        private string? _initError;
        public string? InitError
        {
            get => _initError;
            internal set { _initError = value; OnPropertyChanged(); }
        }

        public MainWindowViewModel(
            INavigationService nav,
            IToastService toast)
        {
            _nav = nav;
            _toast = toast;

            GoLeveranciersCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LeveranciersViewModel>());

            _nav.CurrentViewModelChanged += vm => CurrentViewModel = vm;

            _ = _nav.NavigateToAsync<HomeViewModel>();
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
