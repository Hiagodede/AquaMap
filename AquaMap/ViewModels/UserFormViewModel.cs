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
    public class UserFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        private string _taxId = string.Empty;
        public string TaxId
        {
            get => _taxId;
            set 
            { 
                _taxId = value; 
                OnPropertyChanged(); 
            }
        }

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _phone = string.Empty;
        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        private bool _isAdmin = false;
        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(); }
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

        public ICommand SaveCommand { get; }

        public UserFormViewModel(ApiService apiService)
        {
            _apiService = apiService;
            SaveCommand = new Command(async () => await SaveAsync());
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(TaxId) || string.IsNullOrWhiteSpace(Password))
            {
                IsSuccess = false;
                StatusMessage = "Preencha Nome, CPF e Senha.";
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
                await Task.Delay(1000);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                IsSuccess = false;
                StatusMessage = "Erro ao cadastrar. CPF pode já estar em uso.";
            }

            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
