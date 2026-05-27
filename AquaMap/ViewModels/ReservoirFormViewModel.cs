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
    [QueryProperty(nameof(ReservoirId), "ReservoirId")]
    [QueryProperty(nameof(ReservoirName), "ReservoirName")]
    [QueryProperty(nameof(ReservoirLatitude), "ReservoirLatitude")]
    [QueryProperty(nameof(ReservoirLongitude), "ReservoirLongitude")]
    public class ReservoirFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private int _reservoirId;
        public int ReservoirId
        {
            get => _reservoirId;
            set { _reservoirId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(PageTitle)); }
        }

        private string _reservoirName = string.Empty;
        public string ReservoirName
        {
            get => _reservoirName;
            set 
            { 
                _reservoirName = value; 
                OnPropertyChanged(); 
                SaveDraft();
            }
        }

        private string _reservoirLatitude = string.Empty;
        private string _reservoirLongitude = string.Empty;
        private bool _isFormattingCoord = false;

        public string ReservoirLatitude
        {
            get => _reservoirLatitude;
            set
            {
                if (_isFormattingCoord) { _reservoirLatitude = value; return; }
                _isFormattingCoord = true;
                _reservoirLatitude = SanitizeCoordinate(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
            }
        }

        public string ReservoirLongitude
        {
            get => _reservoirLongitude;
            set
            {
                if (_isFormattingCoord) { _reservoirLongitude = value; return; }
                _isFormattingCoord = true;
                _reservoirLongitude = SanitizeCoordinate(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
            }
        }

        /// <summary>
        /// Permite apenas: dígitos, um "-" no início, um "." como separador decimal, e no máximo 6 casas decimais.
        /// Exemplo válido: -20.763600
        /// </summary>
        private static string SanitizeCoordinate(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            bool hasNegative = false;
            bool hasDecimal = false;
            int decimalDigits = 0;
            var result = new System.Text.StringBuilder();

            foreach (char c in input)
            {
                if (c == '-' && !hasNegative && result.Length == 0)
                {
                    hasNegative = true;
                    result.Append(c);
                }
                else if ((c == '.' || c == ',') && !hasDecimal)
                {
                    hasDecimal = true;
                    result.Append('.');
                }
                else if (char.IsDigit(c))
                {
                    if (hasDecimal)
                    {
                        if (decimalDigits < 6)
                        {
                            result.Append(c);
                            decimalDigits++;
                        }
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
            }

            return result.ToString();
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

        public bool IsEditing => ReservoirId > 0;
        public string PageTitle => IsEditing ? "Editar Reservatório" : "Novo Reservatório";

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand GetLocationCommand { get; }

        public ReservoirFormViewModel(ApiService apiService)
        {
            _apiService = apiService;
            SaveCommand = new Command(async () => await SaveAsync());
            DeleteCommand = new Command(async () => await DeleteAsync());
            GetLocationCommand = new Command(async () => await GetLocationAsync());
        }

        public void InitializeForm()
        {
            if (IsEditing)
            {
                _ = LoadCoordinatesIfNeededAsync();
            }
            else
            {
                LoadDraft();
            }
        }

        private void SaveDraft()
        {
            if (IsEditing) return;
            Preferences.Default.Set("Draft_Reservoir_Name", ReservoirName);
            Preferences.Default.Set("Draft_Reservoir_Latitude", ReservoirLatitude);
            Preferences.Default.Set("Draft_Reservoir_Longitude", ReservoirLongitude);
        }

        private void LoadDraft()
        {
            if (IsEditing) return;
            _reservoirName = Preferences.Default.Get("Draft_Reservoir_Name", string.Empty);
            _reservoirLatitude = Preferences.Default.Get("Draft_Reservoir_Latitude", string.Empty);
            _reservoirLongitude = Preferences.Default.Get("Draft_Reservoir_Longitude", string.Empty);
            OnPropertyChanged(nameof(ReservoirName));
            OnPropertyChanged(nameof(ReservoirLatitude));
            OnPropertyChanged(nameof(ReservoirLongitude));
        }

        private void ClearDraft()
        {
            Preferences.Default.Remove("Draft_Reservoir_Name");
            Preferences.Default.Remove("Draft_Reservoir_Latitude");
            Preferences.Default.Remove("Draft_Reservoir_Longitude");
        }

        private async Task LoadCoordinatesIfNeededAsync()
        {
            if (ReservoirId <= 0) return;
            if (string.IsNullOrWhiteSpace(ReservoirLatitude) || string.IsNullOrWhiteSpace(ReservoirLongitude) ||
                ReservoirLatitude == "0" || ReservoirLongitude == "0")
            {
                IsBusy = true;
                try
                {
                    var reservoirs = await _apiService.GetReservoirsAsync();
                    var res = reservoirs.FirstOrDefault(r => r.Id == ReservoirId);
                    if (res != null)
                    {
                        _isFormattingCoord = true;
                        ReservoirLatitude = res.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        ReservoirLongitude = res.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        _isFormattingCoord = false;
                        OnPropertyChanged(nameof(ReservoirLatitude));
                        OnPropertyChanged(nameof(ReservoirLongitude));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao carregar coordenadas: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(ReservoirName))
            {
                IsSuccess = false;
                StatusMessage = "Informe o nome do reservatório.";
                return;
            }

            if (!double.TryParse(ReservoirLatitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(ReservoirLongitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
            {
                IsSuccess = false;
                StatusMessage = "Coordenadas inválidas. Use formato decimal (ex: -20.7636).";
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
                return;
            }

            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token))
            {
                IsSuccess = false;
                StatusMessage = "Sessão expirada. Faça login novamente.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                bool success;
                if (IsEditing)
                {
                    success = await _apiService.UpdateReservoirAsync(ReservoirId, ReservoirName, lat, lon, token);
                }
                else
                {
                    success = await _apiService.CreateReservoirAsync(ReservoirName, lat, lon, token);
                }

                if (success)
                {
                    IsSuccess = true;
                    StatusMessage = IsEditing ? "Reservatório atualizado!" : "Reservatório cadastrado!";
                    ClearDraft();
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                    await Task.Delay(1000);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = "Erro ao salvar. Verifique conexão e permissões.";
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
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

        private async Task DeleteAsync()
        {
            if (!IsEditing || IsBusy) return;

            bool confirm = await Shell.Current.DisplayAlert("Confirmar", $"Excluir '{ReservoirName}'?", "Sim", "Cancelar");
            if (!confirm) return;

            var token = await SecureStorage.Default.GetAsync("jwt_token");
            if (string.IsNullOrEmpty(token)) return;

            IsBusy = true;
            try
            {
                var success = await _apiService.DeleteReservoirAsync(ReservoirId, token);
                if (success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = "Erro ao excluir reservatório.";
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

        private async Task GetLocationAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = "Buscando localização atual...";
            
            try
            {
                var request = new Microsoft.Maui.Devices.Sensors.GeolocationRequest(Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Microsoft.Maui.Devices.Sensors.Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    _isFormattingCoord = true;
                    ReservoirLatitude = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ReservoirLongitude = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _isFormattingCoord = false;
                    OnPropertyChanged(nameof(ReservoirLatitude));
                    OnPropertyChanged(nameof(ReservoirLongitude));
                    StatusMessage = "Localização capturada com sucesso.";
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                    SaveDraft();
                }
                else
                {
                    StatusMessage = "Não foi possível obter a localização.";
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
                }
            }
            catch (Microsoft.Maui.ApplicationModel.FeatureNotSupportedException)
            {
                StatusMessage = "GPS não suportado neste dispositivo.";
            }
            catch (Microsoft.Maui.ApplicationModel.FeatureNotEnabledException)
            {
                StatusMessage = "Ative o GPS do dispositivo.";
            }
            catch (Microsoft.Maui.ApplicationModel.PermissionException)
            {
                StatusMessage = "Permissão de localização negada.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao obter localização: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
