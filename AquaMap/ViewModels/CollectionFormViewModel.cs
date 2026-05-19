using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Domain.Entities;
using AquaMap.Services;

namespace AquaMap.ViewModels
{
    public class CollectionFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        public ObservableCollection<Reservoir> Reservoirs { get; set; } = new();

        private Reservoir? _selectedReservoir;
        public Reservoir? SelectedReservoir
        {
            get => _selectedReservoir;
            set { _selectedReservoir = value; OnPropertyChanged(); }
        }

        private string _residualChlorine = string.Empty;
        public string ResidualChlorine
        {
            get => _residualChlorine;
            set { _residualChlorine = value; OnPropertyChanged(); }
        }

        private string _ph = string.Empty;
        public string Ph
        {
            get => _ph;
            set { _ph = value; OnPropertyChanged(); }
        }

        private string _turbidity = string.Empty;
        public string Turbidity
        {
            get => _turbidity;
            set { _turbidity = value; OnPropertyChanged(); }
        }

        private bool _eColiAbsent = true;
        public bool EColiAbsent
        {
            get => _eColiAbsent;
            set { _eColiAbsent = value; OnPropertyChanged(); }
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

        // Mantido para compatibilidade, mas agora usamos IsSuccess + DataTriggers
        private string _statusColor = "Red";
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public ICommand SubmitCommand { get; }
        public ICommand LoadReservoirsCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ManageUsersCommand { get; }

        public CollectionFormViewModel(ApiService apiService)
        {
            _apiService = apiService;
            SubmitCommand = new Command(async () => await SubmitAnalysisAsync());
            LoadReservoirsCommand = new Command(async () => await LoadReservoirsAsync());
            LogoutCommand = new Command(async () => await LogoutAsync());
            ManageUsersCommand = new Command(async () => await Shell.Current.GoToAsync("UserListPage"));
        }

        private async Task LoadReservoirsAsync()
        {
            IsBusy = true;
            var data = await _apiService.GetReservoirsAsync();
            Reservoirs.Clear();
            foreach (var res in data)
            {
                Reservoirs.Add(res);
            }
            IsBusy = false;
        }

        private async Task SubmitAnalysisAsync()
        {
            if (SelectedReservoir == null)
            {
                IsSuccess = false;
                StatusMessage = "Selecione um reservatório.";
                return;
            }

            if (!double.TryParse(ResidualChlorine?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double chlorine) ||
                !double.TryParse(Ph?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double phVal) ||
                !double.TryParse(Turbidity?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double turbidityVal))
            {
                IsSuccess = false;
                StatusMessage = "Valores químicos inválidos.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "Sessão expirada. Faça login novamente.";
                IsSuccess = false;
                await LogoutAsync();
                return;
            }

            var analysis = new WaterAnalysis
            {
                AnalysisDate = DateTime.UtcNow, 
                ResidualChlorine = chlorine, 
                Ph = phVal, 
                Turbidity = turbidityVal, 
                EColiAbsent = EColiAbsent, 
                ReservoirId = SelectedReservoir.Id
            };

            var success = await _apiService.SubmitWaterAnalysisAsync(analysis, token);

            if (success)
            {
                IsSuccess = true;
                StatusColor = "Green";
                StatusMessage = "Análise salva com sucesso!";
                // Limpar campos
                ResidualChlorine = ""; Ph = ""; Turbidity = "";
            }
            else
            {
                IsSuccess = false;
                StatusColor = "Red";
                StatusMessage = "Erro ao salvar análise. Verifique conexão e permissões.";
            }

            IsBusy = false;
        }

        private async Task LogoutAsync()
        {
            SecureStorage.Default.Remove("jwt_token");
            await Shell.Current.GoToAsync("..");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
