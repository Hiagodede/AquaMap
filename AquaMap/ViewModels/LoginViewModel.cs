using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Services;

namespace AquaMap.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private string _taxId = string.Empty;
        private string _password = string.Empty;
        private bool _isPasswordHidden = true;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private bool _isFormattingCpf = false;

        public string TaxId
        {
            get => _taxId;
            set
            {
                if (_isFormattingCpf) { _taxId = value; return; }

                // Extrai apenas dígitos
                string digits = new string((value ?? "").Where(char.IsDigit).ToArray());

                // Limita a 11 dígitos
                if (digits.Length > 11)
                    digits = digits.Substring(0, 11);

                // Aplica a máscara 000.000.000-00
                string formatted = digits;
                if (digits.Length > 9)
                    formatted = $"{digits.Substring(0, 3)}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits.Substring(9)}";
                else if (digits.Length > 6)
                    formatted = $"{digits.Substring(0, 3)}.{digits.Substring(3, 3)}.{digits.Substring(6)}";
                else if (digits.Length > 3)
                    formatted = $"{digits.Substring(0, 3)}.{digits.Substring(3)}";

                _isFormattingCpf = true;
                _taxId = formatted;
                OnPropertyChanged();
                _isFormattingCpf = false;
            }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public bool IsPasswordHidden
        {
            get => _isPasswordHidden;
            set { _isPasswordHidden = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
        }

        public bool IsNotBusy => !IsBusy;

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public ICommand LoginCommand { get; }
        public ICommand TogglePasswordCommand { get; }

        public LoginViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoginCommand = new Command(async () => await PerformLoginAsync());
            TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
        }

        private async Task PerformLoginAsync()
        {
            if (string.IsNullOrWhiteSpace(TaxId) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Preencha CPF e Senha.";
                return;
            }

            // Remove tudo que não for número e formata para o padrão do banco (000.000.000-00)
            string cleanCpf = new string(TaxId.Where(char.IsDigit).ToArray());
            string formattedCpf = TaxId;
            if (cleanCpf.Length == 11)
            {
                formattedCpf = $@"{cleanCpf.Substring(0, 3)}.{cleanCpf.Substring(3, 3)}.{cleanCpf.Substring(6, 3)}-{cleanCpf.Substring(9, 2)}";
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            var token = await _apiService.LoginAsync(formattedCpf, Password);

            if (!string.IsNullOrEmpty(token))
            {
                // Armazena no cofre do sistema operacional (Android/Windows)
                await SecureStorage.Default.SetAsync("jwt_token", token);
                
                // Navega para o formulário de coleta (relativo)
                await Shell.Current.GoToAsync("CollectionFormPage");
            }
            else
            {
                ErrorMessage = "Credenciais inválidas ou erro no servidor.";
            }

            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
