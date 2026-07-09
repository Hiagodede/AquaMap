using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Services;
using AquaMap.Client.Shared;

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
        private string _taxIdWarning = string.Empty;
        private string _passwordWarning = string.Empty;

        public string TaxId
        {
            get => _taxId;
            set 
            { 
                _taxId = value; 
                OnPropertyChanged(); 
                Preferences.Default.Set("Draft_Login_TaxId", value);
                _ = ValidateTaxIdAsync();
            }
        }

        public string Password
        {
            get => _password;
            set 
            { 
                _password = value; 
                OnPropertyChanged(); 
                _ = ValidatePasswordAsync();
            }
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

        public string TaxIdWarning
        {
            get => _taxIdWarning;
            set { _taxIdWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTaxIdValid)); }
        }

        public bool IsTaxIdValid => string.IsNullOrEmpty(TaxIdWarning);

        public string PasswordWarning
        {
            get => _passwordWarning;
            set { _passwordWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPasswordValid)); }
        }

        public bool IsPasswordValid => string.IsNullOrEmpty(PasswordWarning);

        public ICommand LoginCommand { get; }
        public ICommand TogglePasswordCommand { get; }

        public LoginViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoginCommand = new Command(async () => await PerformLoginAsync());
            TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
            
            // Carrega rascunho de CPF salvo pós-background
            _taxId = Preferences.Default.Get("Draft_Login_TaxId", string.Empty);
        }

        private System.Threading.CancellationTokenSource? _taxIdCts;
        private async Task ValidateTaxIdAsync()
        {
            _taxIdCts?.Cancel();
            _taxIdCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _taxIdCts.Token);
                string cleanCpf = new string(TaxId.Where(char.IsDigit).ToArray());
                if (string.IsNullOrWhiteSpace(TaxId))
                {
                    TaxIdWarning = string.Empty;
                }
                else if (cleanCpf.Length != 11 || !IsCpfValid(cleanCpf))
                {
                    TaxIdWarning = "CPF inválido (deve conter 11 dígitos válidos).";
                }
                else
                {
                    TaxIdWarning = string.Empty;
                }
            }
            catch (OperationCanceledException) { }
        }

        private System.Threading.CancellationTokenSource? _passwordCts;
        private async Task ValidatePasswordAsync()
        {
            _passwordCts?.Cancel();
            _passwordCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _passwordCts.Token);
                if (string.IsNullOrWhiteSpace(Password))
                {
                    PasswordWarning = string.Empty;
                }
                else if (Password.Length < 6)
                {
                    PasswordWarning = "Senha deve conter pelo menos 6 caracteres.";
                }
                else
                {
                    PasswordWarning = string.Empty;
                }
            }
            catch (OperationCanceledException) { }
        }

        private static bool IsCpfValid(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return false;
            if (cpf.Distinct().Count() == 1) return false;

            int[] multiplier1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] multiplier2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            string tempCpf = cpf.Substring(0, 9);
            int sum = 0;

            for (int i = 0; i < 9; i++)
                sum += (tempCpf[i] - '0') * multiplier1[i];

            int remainder = sum % 11;
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            string digit = remainder.ToString();
            tempCpf = tempCpf + digit;
            sum = 0;
            for (int i = 0; i < 10; i++)
                sum += (tempCpf[i] - '0') * multiplier2[i];

            remainder = sum % 11;
            if (remainder < 2)
                remainder = 0;
            else
                remainder = 11 - remainder;

            digit = digit + remainder.ToString();
            return cpf.EndsWith(digit);
        }

        private async Task PerformLoginAsync()
        {
            if (IsBusy) return;

            string cleanCpf = new string(TaxId.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(TaxId) || cleanCpf.Length != 11 || !IsCpfValid(cleanCpf))
            {
                ErrorMessage = "Por favor, insira um CPF válido.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            {
                ErrorMessage = "A senha deve conter pelo menos 6 caracteres.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                // Remove tudo que não for número e formata para o padrão do banco (000.000.000-00)
                string formattedCpf = $"{cleanCpf.Substring(0, 3)}.{cleanCpf.Substring(3, 3)}.{cleanCpf.Substring(6, 3)}-{cleanCpf.Substring(9, 2)}";

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
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erro de conexão. Verifique o servidor.";
                System.Diagnostics.Debug.WriteLine($"Erro no login: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
