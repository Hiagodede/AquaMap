using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
            set 
            { 
                _selectedReservoir = value; 
                OnPropertyChanged(); 
                SaveDraft(); 
            }
        }

        private string _residualChlorine = string.Empty;
        public string ResidualChlorine
        {
            get => _residualChlorine;
            set
            {
                _residualChlorine = value;
                OnPropertyChanged();
                SaveDraft();
                _ = ValidateChlorineAsync();
            }
        }

        private bool _isChlorineValid = true;
        public bool IsChlorineValid
        {
            get => _isChlorineValid;
            set { _isChlorineValid = value; OnPropertyChanged(); }
        }

        private string _chlorineWarning = string.Empty;
        public string ChlorineWarning
        {
            get => _chlorineWarning;
            set { _chlorineWarning = value; OnPropertyChanged(); }
        }

        private string _ph = string.Empty;
        public string Ph
        {
            get => _ph;
            set
            {
                _ph = value;
                OnPropertyChanged();
                SaveDraft();
                _ = ValidatePhAsync();
            }
        }

        private bool _isPhValid = true;
        public bool IsPhValid
        {
            get => _isPhValid;
            set { _isPhValid = value; OnPropertyChanged(); }
        }

        private string _phWarning = string.Empty;
        public string PhWarning
        {
            get => _phWarning;
            set { _phWarning = value; OnPropertyChanged(); }
        }

        private string _turbidity = string.Empty;
        public string Turbidity
        {
            get => _turbidity;
            set
            {
                _turbidity = value;
                OnPropertyChanged();
                SaveDraft();
                _ = ValidateTurbidityAsync();
            }
        }

        private bool _isTurbidityValid = true;
        public bool IsTurbidityValid
        {
            get => _isTurbidityValid;
            set { _isTurbidityValid = value; OnPropertyChanged(); }
        }

        private string _turbidityWarning = string.Empty;
        public string TurbidityWarning
        {
            get => _turbidityWarning;
            set { _turbidityWarning = value; OnPropertyChanged(); }
        }

        private bool _eColiAbsent = true;
        public bool EColiAbsent
        {
            get => _eColiAbsent;
            set 
            { 
                _eColiAbsent = value; 
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
            ManageUsersCommand = new Command(async () => 
            {
                if (IsBusy) return;
                IsBusy = true;
                try
                {
                    await Shell.Current.GoToAsync("UserListPage");
                }
                finally
                {
                    IsBusy = false;
                }
            });
            LoadDraft();
        }

        private void SaveDraft()
        {
            Preferences.Default.Set("Draft_Collection_ReservoirId", SelectedReservoir?.Id ?? -1);
            Preferences.Default.Set("Draft_Collection_Chlorine", ResidualChlorine);
            Preferences.Default.Set("Draft_Collection_Ph", Ph);
            Preferences.Default.Set("Draft_Collection_Turbidity", Turbidity);
            Preferences.Default.Set("Draft_Collection_EColiAbsent", EColiAbsent);
        }

        private void LoadDraft()
        {
            _residualChlorine = Preferences.Default.Get("Draft_Collection_Chlorine", string.Empty);
            _ph = Preferences.Default.Get("Draft_Collection_Ph", string.Empty);
            _turbidity = Preferences.Default.Get("Draft_Collection_Turbidity", string.Empty);
            _eColiAbsent = Preferences.Default.Get("Draft_Collection_EColiAbsent", true);
        }

        private void ClearDraft()
        {
            Preferences.Default.Remove("Draft_Collection_ReservoirId");
            Preferences.Default.Remove("Draft_Collection_Chlorine");
            Preferences.Default.Remove("Draft_Collection_Ph");
            Preferences.Default.Remove("Draft_Collection_Turbidity");
            Preferences.Default.Remove("Draft_Collection_EColiAbsent");
        }

        private System.Threading.CancellationTokenSource? _chlorineCts;
        private async Task ValidateChlorineAsync()
        {
            _chlorineCts?.Cancel();
            _chlorineCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _chlorineCts.Token);
                if (string.IsNullOrWhiteSpace(ResidualChlorine))
                {
                    IsChlorineValid = true;
                    ChlorineWarning = string.Empty;
                }
                else if (double.TryParse(ResidualChlorine.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    IsChlorineValid = val >= 0.2 && val <= 2.0;
                    ChlorineWarning = IsChlorineValid ? string.Empty : "Ideal: 0.2 a 2.0 mg/L (Portaria 888)";
                }
                else
                {
                    IsChlorineValid = false;
                    ChlorineWarning = "Valor inválido.";
                }
            }
            catch (OperationCanceledException) { }
        }

        private System.Threading.CancellationTokenSource? _phCts;
        private async Task ValidatePhAsync()
        {
            _phCts?.Cancel();
            _phCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _phCts.Token);
                if (string.IsNullOrWhiteSpace(Ph))
                {
                    IsPhValid = true;
                    PhWarning = string.Empty;
                }
                else if (double.TryParse(Ph.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    IsPhValid = val >= 6.0 && val <= 9.5;
                    PhWarning = IsPhValid ? string.Empty : "Ideal: 6.0 a 9.5 (Portaria 888)";
                }
                else
                {
                    IsPhValid = false;
                    PhWarning = "Valor inválido.";
                }
            }
            catch (OperationCanceledException) { }
        }

        private System.Threading.CancellationTokenSource? _turbidityCts;
        private async Task ValidateTurbidityAsync()
        {
            _turbidityCts?.Cancel();
            _turbidityCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _turbidityCts.Token);
                if (string.IsNullOrWhiteSpace(Turbidity))
                {
                    IsTurbidityValid = true;
                    TurbidityWarning = string.Empty;
                }
                else if (double.TryParse(Turbidity.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    IsTurbidityValid = val <= 5.0 && val >= 0.0;
                    TurbidityWarning = IsTurbidityValid ? string.Empty : "Ideal: ≤ 5.0 NTU (Portaria 888)";
                }
                else
                {
                    IsTurbidityValid = false;
                    TurbidityWarning = "Valor inválido.";
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task LoadReservoirsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var data = await _apiService.GetReservoirsAsync();
                Reservoirs.Clear();
                foreach (var res in data)
                {
                    Reservoirs.Add(res);
                }

                // Restore selected reservoir from draft
                int savedId = Preferences.Default.Get("Draft_Collection_ReservoirId", -1);
                if (savedId != -1)
                {
                    SelectedReservoir = Reservoirs.FirstOrDefault(r => r.Id == savedId);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SubmitAnalysisAsync()
        {
            if (IsBusy) return;

            if (SelectedReservoir == null)
            {
                IsSuccess = false;
                StatusMessage = "Selecione um reservatório.";
                return;
            }

            if (!IsChlorineValid || !IsPhValid || !IsTurbidityValid)
            {
                IsSuccess = false;
                StatusMessage = "Corrija os parâmetros fora do padrão antes de enviar.";
                return;
            }

            if (!double.TryParse(ResidualChlorine?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double chlorine) ||
                !double.TryParse(Ph?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double phVal) ||
                !double.TryParse(Turbidity?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double turbidityVal))
            {
                IsSuccess = false;
                StatusMessage = "Os parâmetros devem ser valores numéricos válidos.";
                return;
            }

            if (chlorine < 0)
            {
                IsSuccess = false;
                StatusMessage = "O cloro residual não pode ser negativo.";
                return;
            }

            if (phVal < 0 || phVal > 14)
            {
                IsSuccess = false;
                StatusMessage = "O pH deve ser um valor entre 0.0 e 14.0.";
                return;
            }

            if (turbidityVal < 0)
            {
                IsSuccess = false;
                StatusMessage = "A turbidez não pode ser negativa.";
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

            try
            {
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
                    
                    // Limpar campos e rascunho
                    ClearDraft();
                    _residualChlorine = ""; 
                    _ph = ""; 
                    _turbidity = "";
                    _selectedReservoir = null;
                    _eColiAbsent = true;
                    OnPropertyChanged(nameof(ResidualChlorine));
                    OnPropertyChanged(nameof(Ph));
                    OnPropertyChanged(nameof(Turbidity));
                    OnPropertyChanged(nameof(SelectedReservoir));
                    OnPropertyChanged(nameof(EColiAbsent));

                    // Reset warnings
                    IsChlorineValid = true;
                    ChlorineWarning = string.Empty;
                    IsPhValid = true;
                    PhWarning = string.Empty;
                    IsTurbidityValid = true;
                    TurbidityWarning = string.Empty;
                }
                else
                {
                    IsSuccess = false;
                    StatusColor = "Red";
                    StatusMessage = "Erro ao salvar análise. Verifique conexão e permissões.";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusColor = "Red";
                StatusMessage = $"Erro de conexão: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LogoutAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                SecureStorage.Default.Remove("jwt_token");
                await Shell.Current.GoToAsync("..");
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
