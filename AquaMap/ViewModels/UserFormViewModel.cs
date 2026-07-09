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
    public class UserFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set 
            { 
                _fullName = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
                _ = ValidateFullNameAsync(); 
            }
        }

        private string _taxId = string.Empty;
        public string TaxId
        {
            get => _taxId;
            set 
            { 
                _taxId = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
                _ = ValidateTaxIdAsync(); 
            }
        }

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set 
            { 
                _email = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
                _ = ValidateEmailAsync(); 
            }
        }

        private string _phone = string.Empty;
        public string Phone
        {
            get => _phone;
            set 
            { 
                _phone = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
                _ = ValidatePhoneAsync(); 
            }
        }

        private string _password = string.Empty;
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

        private bool _isAdmin = false;
        public bool IsAdmin
        {
            get => _isAdmin;
            set 
            { 
                _isAdmin = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !IsBusy;

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); }
        }

        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }
        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        // Warning messages for UI
        private string _fullNameWarning = string.Empty;
        public string FullNameWarning
        {
            get => _fullNameWarning;
            set { _fullNameWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFullNameValid)); }
        }
        public bool IsFullNameValid => string.IsNullOrEmpty(FullNameWarning);

        private string _taxIdWarning = string.Empty;
        public string TaxIdWarning
        {
            get => _taxIdWarning;
            set { _taxIdWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTaxIdValid)); }
        }
        public bool IsTaxIdValid => string.IsNullOrEmpty(TaxIdWarning);

        private string _emailWarning = string.Empty;
        public string EmailWarning
        {
            get => _emailWarning;
            set { _emailWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmailValid)); }
        }
        public bool IsEmailValid => string.IsNullOrEmpty(EmailWarning);

        private string _phoneWarning = string.Empty;
        public string PhoneWarning
        {
            get => _phoneWarning;
            set { _phoneWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPhoneValid)); }
        }
        public bool IsPhoneValid => string.IsNullOrEmpty(PhoneWarning);

        private string _passwordWarning = string.Empty;
        public string PasswordWarning
        {
            get => _passwordWarning;
            set { _passwordWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPasswordValid)); }
        }
        public bool IsPasswordValid => string.IsNullOrEmpty(PasswordWarning);

        public ICommand SaveCommand { get; }

        public UserFormViewModel(ApiService apiService)
        {
            _apiService = apiService;
            SaveCommand = new Command(async () => await SaveAsync());
            LoadDraft();
        }

        private void SaveDraft()
        {
            Preferences.Default.Set("Draft_UserForm_FullName", FullName);
            Preferences.Default.Set("Draft_UserForm_TaxId", TaxId);
            Preferences.Default.Set("Draft_UserForm_Email", Email);
            Preferences.Default.Set("Draft_UserForm_Phone", Phone);
            Preferences.Default.Set("Draft_UserForm_IsAdmin", IsAdmin);
        }

        private void LoadDraft()
        {
            _fullName = Preferences.Default.Get("Draft_UserForm_FullName", string.Empty);
            _taxId = Preferences.Default.Get("Draft_UserForm_TaxId", string.Empty);
            _email = Preferences.Default.Get("Draft_UserForm_Email", string.Empty);
            _phone = Preferences.Default.Get("Draft_UserForm_Phone", string.Empty);
            _isAdmin = Preferences.Default.Get("Draft_UserForm_IsAdmin", false);
        }

        private void ClearDraft()
        {
            Preferences.Default.Remove("Draft_UserForm_FullName");
            Preferences.Default.Remove("Draft_UserForm_TaxId");
            Preferences.Default.Remove("Draft_UserForm_Email");
            Preferences.Default.Remove("Draft_UserForm_Phone");
            Preferences.Default.Remove("Draft_UserForm_IsAdmin");
        }

        private System.Threading.CancellationTokenSource? _fullNameCts;
        private async Task ValidateFullNameAsync()
        {
            _fullNameCts?.Cancel();
            _fullNameCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _fullNameCts.Token);
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    FullNameWarning = string.Empty;
                }
                else if (!FullName.Trim().Contains(" ") || FullName.Trim().Split(' ').Length < 2)
                {
                    FullNameWarning = "Por favor, insira o nome completo (nome e sobrenome).";
                }
                else
                {
                    FullNameWarning = string.Empty;
                }
            }
            catch (OperationCanceledException) { }
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

        private System.Threading.CancellationTokenSource? _emailCts;
        private async Task ValidateEmailAsync()
        {
            _emailCts?.Cancel();
            _emailCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _emailCts.Token);
                if (string.IsNullOrWhiteSpace(Email))
                {
                    EmailWarning = string.Empty;
                }
                else if (!Email.Contains("@") || !Email.Contains("."))
                {
                    EmailWarning = "E-mail inválido (formato incorreto).";
                }
                else
                {
                    EmailWarning = string.Empty;
                }
            }
            catch (OperationCanceledException) { }
        }

        private System.Threading.CancellationTokenSource? _phoneCts;
        private async Task ValidatePhoneAsync()
        {
            _phoneCts?.Cancel();
            _phoneCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _phoneCts.Token);
                string cleanPhone = new string(Phone.Where(char.IsDigit).ToArray());
                if (string.IsNullOrWhiteSpace(Phone))
                {
                    PhoneWarning = string.Empty;
                }
                else if (cleanPhone.Length < 10)
                {
                    PhoneWarning = "Telefone deve conter pelo menos 10 dígitos (com DDD).";
                }
                else
                {
                    PhoneWarning = string.Empty;
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

        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(TaxId) || string.IsNullOrWhiteSpace(Password))
            {
                IsSuccess = false;
                StatusMessage = "Preencha Nome, CPF e Senha.";
                return;
            }

            if (!IsFullNameValid || !IsTaxIdValid || !IsEmailValid || !IsPhoneValid || !IsPasswordValid)
            {
                IsSuccess = false;
                StatusMessage = "Corrija os campos inválidos antes de salvar.";
                return;
            }

            string cleanCpf = new string(TaxId.Where(char.IsDigit).ToArray());
            if (cleanCpf.Length != 11)
            {
                IsSuccess = false;
                StatusMessage = "CPF deve ter 11 dígitos.";
                return;
            }

            string formattedCpf = $"{cleanCpf.Substring(0, 3)}.{cleanCpf.Substring(3, 3)}.{cleanCpf.Substring(6, 3)}-{cleanCpf.Substring(9, 2)}";

            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token))
            {
                IsSuccess = false;
                StatusMessage = "Sessão expirada.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var userData = new
                {
                    FullName = FullName,
                    TaxId = formattedCpf,
                    BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Address = "N/A",
                    PhoneNumber = Phone ?? "N/A",
                    Email = Email ?? "N/A",
                    Password = Password,
                    Role = IsAdmin ? 1 : 0
                };

                var success = await _apiService.CreateUserAsync(userData, token);

                if (success)
                {
                    IsSuccess = true;
                    StatusMessage = "Técnico cadastrado com sucesso!";
                    ClearDraft();
                    await Task.Delay(1000);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = "Erro ao cadastrar. CPF pode já estar em uso.";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"Erro de conexão: {ex.Message}";
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
