using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Interfaces;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace QuadroApp.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _nav;
        private readonly IToastService _toast;

        public IToastService Toast => _toast;
        public IAsyncRelayCommand GoLeveranciersCommand { get; }

        /// <summary>Displays the running assembly version, e.g. "v1.0.4".</summary>
        public string AppVersion
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
            IToastService toast,
            IAuthService auth)
        {
            _nav = nav;
            _toast = toast;
            _auth = auth;

            GoLeveranciersCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LeveranciersViewModel>());
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            VergrendelCommand = new RelayCommand(Vergrendel);

            _nav.CurrentViewModelChanged += vm => CurrentViewModel = vm;

            // US-32: auto-lock na inactiviteit (standaard 15 min)
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _idleTimer.Tick += (_, _) =>
            {
                if (!IsLocked && DateTime.Now - _laatsteActiviteit > TimeSpan.FromMinutes(LockNaMinuten))
                    Vergrendel();
            };
            _idleTimer.Start();

            _ = _nav.NavigateToAsync<HomeViewModel>();
        }

        // ══════════════ US-32: login / vergrendeling ══════════════

        private readonly IAuthService _auth;
        private readonly DispatcherTimer _idleTimer;
        private DateTime _laatsteActiviteit = DateTime.Now;

        /// <summary>Auto-lock drempel in minuten.</summary>
        public int LockNaMinuten { get; set; } = 15;

        /// <summary>Gefired na login wanneer het wachtwoord verplicht gewijzigd moet worden (MainWindow opent de dialoog).</summary>
        public event EventHandler? WachtwoordWijzigenVereist;

        public IAsyncRelayCommand LoginCommand { get; }
        public IRelayCommand VergrendelCommand { get; }

        private bool _isLocked = true;
        /// <summary>True zolang niemand is ingelogd — toont het login-overlay.</summary>
        public bool IsLocked
        {
            get => _isLocked;
            private set { _isLocked = value; OnPropertyChanged(); }
        }

        private string _loginGebruikersnaam = "";
        public string LoginGebruikersnaam
        {
            get => _loginGebruikersnaam;
            set { _loginGebruikersnaam = value; OnPropertyChanged(); }
        }

        private string _loginWachtwoord = "";
        public string LoginWachtwoord
        {
            get => _loginWachtwoord;
            set { _loginWachtwoord = value; OnPropertyChanged(); }
        }

        private string? _loginFout;
        public string? LoginFout
        {
            get => _loginFout;
            private set { _loginFout = value; OnPropertyChanged(); }
        }

        private string _ingelogdeGebruiker = "";
        public string IngelogdeGebruiker
        {
            get => _ingelogdeGebruiker;
            private set { _ingelogdeGebruiker = value; OnPropertyChanged(); }
        }

        private async System.Threading.Tasks.Task LoginAsync()
        {
            LoginFout = null;
            var fout = await _auth.LoginAsync(LoginGebruikersnaam, LoginWachtwoord);
            if (fout is not null)
            {
                LoginFout = fout;
                return;
            }

            LoginWachtwoord = "";
            IngelogdeGebruiker = _auth.CurrentUser?.VolledigeNaam ?? _auth.CurrentUser?.GebruikersNaam ?? "";
            _laatsteActiviteit = DateTime.Now;
            IsLocked = false;

            if (_auth.CurrentUser?.MoetWachtwoordWijzigen == true)
            {
                _toast.Warning("Wijzig het standaardwachtwoord (verplicht bij eerste gebruik).");
                WachtwoordWijzigenVereist?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Vergrendel()
        {
            _auth.Logout();
            LoginWachtwoord = "";
            IsLocked = true;
        }

        /// <summary>Aangeroepen vanuit MainWindow bij muis/toetsenbord-activiteit.</summary>
        public void RegistreerActiviteit() => _laatsteActiviteit = DateTime.Now;


        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
