// Candidate for removal – requires runtime verification
﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
namespace QuadroApp.ViewModels
{




    [Obsolete("Not used in current startup flow. Remove after runtime verification.")]
    public partial class LoginViewModel : ObservableObject
    {
        private readonly Action _onSuccess;

        [ObservableProperty]
        private string password = "";

        [ObservableProperty]
        private string errorMessage = "";

        public IAsyncRelayCommand LoginCommand { get; }

        public LoginViewModel(Action onSuccess)
        {
            _onSuccess = onSuccess;
            LoginCommand = new AsyncRelayCommand(LoginAsync);
        }

        private async Task LoginAsync()
        {
            await Task.Delay(50); // kleine async flow

            var envPassword = Environment.GetEnvironmentVariable("QUADRO_APP_PASSWORD");

            if (string.IsNullOrWhiteSpace(envPassword))
            {
                ErrorMessage = "Environment variable niet ingesteld.";
                return;
            }

            if (Password == envPassword)
            {
                ErrorMessage = "";
                _onSuccess();
            }
            else
            {
                ErrorMessage = "Onjuist wachtwoord.";
            }
        }
    }
}
