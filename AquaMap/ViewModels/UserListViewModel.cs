using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Services;
using AquaMap.Client.Shared;

namespace AquaMap.ViewModels
{
    public class UserListViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        public ObservableCollection<UserDto> Users { get; set; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            set { _hasData = value; OnPropertyChanged(); }
        }

        public ICommand LoadUsersCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand DeleteUserCommand { get; }

        public UserListViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoadUsersCommand = new Command(async () => await LoadUsersAsync());
            AddUserCommand = new Command(async () => 
            {
                if (IsBusy) return;
                IsBusy = true;
                try
                {
                    await Shell.Current.GoToAsync("UserFormPage");
                }
                finally
                {
                    IsBusy = false;
                }
            });
            DeleteUserCommand = new Command<UserDto>(async (user) => await DeleteUserAsync(user));
        }

        public async Task LoadUsersAsync()
        {
            if (IsBusy) return;
            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token)) return;

            IsBusy = true;
            try
            {
                var data = await _apiService.GetUsersAsync(token);
                Users.Clear();
                foreach (var user in data)
                {
                    Users.Add(user);
                }
                HasData = Users.Count > 0;
                IsEmpty = !HasData;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteUserAsync(UserDto? user)
        {
            if (user == null || IsBusy) return;

            bool confirm = await Shell.Current.DisplayAlert("Confirmar", $"Excluir '{user.FullName}'?", "Sim", "Cancelar");
            if (!confirm) return;

            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token)) return;

            IsBusy = true;
            try
            {
                var result = await _apiService.DeleteUserAsync(user.Id, token);
                if (result.Success)
                {
                    Users.Remove(user);
                    HasData = Users.Count > 0;
                    IsEmpty = !HasData;
                }
                else
                {
                    var message = result.StatusCode switch
                    {
                        409 when !string.IsNullOrWhiteSpace(result.ErrorMessage) => result.ErrorMessage!,
                        401 => "Sessão expirada. Faça login novamente.",
                        null => "Não foi possível excluir o usuário. Verifique sua conexão e tente novamente.",
                        _ => "Não foi possível excluir o usuário."
                    };
                    await Shell.Current.DisplayAlert("Erro", message, "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao excluir usuário: {ex}");
                try
                {
                    await Shell.Current.DisplayAlert("Erro", "Não foi possível excluir o usuário. Tente novamente.", "OK");
                }
                catch (Exception alertEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Falha ao exibir alerta: {alertEx}");
                }
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
