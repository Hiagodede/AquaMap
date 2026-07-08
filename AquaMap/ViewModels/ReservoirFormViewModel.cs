using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Services;

namespace AquaMap.ViewModels
{
    /// <summary>
    /// Item do checklist de bairros atendidos pelo reservatório.
    /// </summary>
    public class NeighborhoodCheckItem : INotifyPropertyChanged
    {
        public string Name { get; init; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ICommand ToggleCommand { get; }

        public NeighborhoodCheckItem()
        {
            ToggleCommand = new Command(() =>
            {
                IsSelected = !IsSelected;
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    [QueryProperty(nameof(ReservoirId), "ReservoirId")]
    [QueryProperty(nameof(ReservoirName), "ReservoirName")]
    [QueryProperty(nameof(ReservoirLatitude), "ReservoirLatitude")]
    [QueryProperty(nameof(ReservoirLongitude), "ReservoirLongitude")]
    public class ReservoirFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        // ── Lista oficial de pontos de coleta do SAAE ──────────────────────────
        private static readonly string[] AllNeighborhoods =
        {
            "Clerio Moulin",
            "Loteamento Boa Fé",
            "Charqueada",
            "Pedro Martins Rua 13",
            "Samarco",
            "Linha Amarela",
            "Alto Universitário (Rua Felício Alcure)",
            "Guararema",
            "Colina (Ginásio de Esportes)",
            "Centro",
            "Rua do Norte",
            "Vila Alta",
            "BR 482",
            "Cila Machado",
            "Querosene",
            "Loteamento Antônio Lemos Jr",
            "Paiinha",
            "Campo de Aviação"
        };

        // ── Propriedades da form ───────────────────────────────────────────────
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
            set { _reservoirName = value; OnPropertyChanged(); SaveDraft(); }
        }

        // ── Coordenadas (internas) ─────────────────────────────────────────────
        private double _latitude;
        private double _longitude;
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

        // ── UX de Localização ──────────────────────────────────────────────────
        private bool _locationCaptured;
        public bool LocationCaptured
        {
            get => _locationCaptured;
            set { _locationCaptured = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocationNotCaptured)); }
        }
        public bool LocationNotCaptured => !_locationCaptured;

        private string _locationDisplay = string.Empty;
        public string LocationDisplay
        {
            get => _locationDisplay;
            set { _locationDisplay = value; OnPropertyChanged(); }
        }

        private bool _isMapPickerVisible;
        public bool IsMapPickerVisible
        {
            get => _isMapPickerVisible;
            set { _isMapPickerVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(MapPickerButtonText)); }
        }
        public string MapPickerButtonText => IsMapPickerVisible ? "🗺️ Fechar mapa" : "🗺️ Marcar no mapa manualmente";

        private bool _isManualInputVisible;
        public bool IsManualInputVisible
        {
            get => _isManualInputVisible;
            set { _isManualInputVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(ManualInputButtonText)); }
        }
        public string ManualInputButtonText => IsManualInputVisible ? "▲ Ocultar campos manuais" : "Inserir coordenadas manualmente";

        private bool _isCapturingGps;
        public bool IsCapturingGps
        {
            get => _isCapturingGps;
            set { _isCapturingGps = value; OnPropertyChanged(); }
        }

        // ── Bairros ────────────────────────────────────────────────────────────
        public ObservableCollection<NeighborhoodCheckItem> Neighborhoods { get; } = new();

        public int SelectedNeighborhoodsCount => Neighborhoods.Count(n => n.IsSelected);

        // ── Status / Busy ──────────────────────────────────────────────────────
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

        // ── Comandos ───────────────────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CaptureLocationCommand { get; }
        public ICommand ToggleMapPickerCommand { get; }
        public ICommand ToggleManualInputCommand { get; }

        public ReservoirFormViewModel(ApiService apiService)
        {
            _apiService = apiService;

            // Popula lista de bairros
            foreach (var name in AllNeighborhoods)
            {
                var item = new NeighborhoodCheckItem { Name = name };
                item.PropertyChanged += (_, __) => OnPropertyChanged(nameof(SelectedNeighborhoodsCount));
                Neighborhoods.Add(item);
            }

            SaveCommand = new Command(async () => await SaveAsync());
            DeleteCommand = new Command(async () => await DeleteAsync());
            CaptureLocationCommand = new Command(async () => await CaptureGpsLocationAsync());
            ToggleMapPickerCommand = new Command(() =>
            {
                IsMapPickerVisible = !IsMapPickerVisible;
                if (IsMapPickerVisible) IsManualInputVisible = false;
            });
            ToggleManualInputCommand = new Command(() =>
            {
                IsManualInputVisible = !IsManualInputVisible;
                if (IsManualInputVisible) IsMapPickerVisible = false;
            });
        }

        public void InitializeForm()
        {
            if (IsEditing)
            {
                _ = LoadReservoirDataAsync();
            }
            else
            {
                LoadDraft();
            }
        }

        /// <summary>
        /// Chamado pelo code-behind quando o usuário toca no mapa do seletor.
        /// </summary>
        public void SetLocationFromMap(double lat, double lng)
        {
            _latitude = lat;
            _longitude = lng;
            _isFormattingCoord = true;
            ReservoirLatitude = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ReservoirLongitude = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _isFormattingCoord = false;
            OnPropertyChanged(nameof(ReservoirLatitude));
            OnPropertyChanged(nameof(ReservoirLongitude));
            LocationDisplay = $"📍 {lat:F5}, {lng:F5}";
            LocationCaptured = true;
            IsMapPickerVisible = false;
            try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
        }

        // ── GPS ────────────────────────────────────────────────────────────────
        private async Task CaptureGpsLocationAsync()
        {
            if (IsCapturingGps) return;
            IsCapturingGps = true;
            LocationDisplay = "Buscando localização...";
            LocationCaptured = false;

            try
            {
                var request = new Microsoft.Maui.Devices.Sensors.GeolocationRequest(
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Medium,
                    TimeSpan.FromSeconds(10));
                var location = await Microsoft.Maui.Devices.Sensors.Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    _latitude = location.Latitude;
                    _longitude = location.Longitude;
                    _isFormattingCoord = true;
                    ReservoirLatitude = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ReservoirLongitude = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _isFormattingCoord = false;
                    OnPropertyChanged(nameof(ReservoirLatitude));
                    OnPropertyChanged(nameof(ReservoirLongitude));
                    LocationDisplay = $"📍 {location.Latitude:F5}, {location.Longitude:F5}";
                    LocationCaptured = true;
                    SaveDraft();
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                }
                else
                {
                    LocationDisplay = "Localização não disponível. Tente novamente.";
                }
            }
            catch (Microsoft.Maui.ApplicationModel.FeatureNotEnabledException)
            {
                LocationDisplay = "Ative o GPS do dispositivo.";
            }
            catch (Microsoft.Maui.ApplicationModel.PermissionException)
            {
                LocationDisplay = "Permissão de localização negada.";
            }
            catch (Exception)
            {
                LocationDisplay = "Erro ao obter localização.";
            }
            finally
            {
                IsCapturingGps = false;
            }
        }

        // ── Draft ──────────────────────────────────────────────────────────────
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

            // Restaurar estado de localização se havia coordenadas salvas
            if (!string.IsNullOrWhiteSpace(_reservoirLatitude) && !string.IsNullOrWhiteSpace(_reservoirLongitude))
            {
                LocationDisplay = $"📍 {_reservoirLatitude}, {_reservoirLongitude}";
                LocationCaptured = true;
            }
        }

        private void ClearDraft()
        {
            Preferences.Default.Remove("Draft_Reservoir_Name");
            Preferences.Default.Remove("Draft_Reservoir_Latitude");
            Preferences.Default.Remove("Draft_Reservoir_Longitude");
        }

        private async Task LoadReservoirDataAsync()
        {
            if (ReservoirId <= 0) return;
            IsBusy = true;
            try
            {
                var reservoirs = await _apiService.GetReservoirsAsync();
                var res = reservoirs.FirstOrDefault(r => r.Id == ReservoirId);
                if (res != null)
                {
                    // Coordenadas
                    _latitude = res.Latitude;
                    _longitude = res.Longitude;
                    _isFormattingCoord = true;
                    ReservoirLatitude = res.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ReservoirLongitude = res.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _isFormattingCoord = false;
                    OnPropertyChanged(nameof(ReservoirLatitude));
                    OnPropertyChanged(nameof(ReservoirLongitude));
                    LocationDisplay = $"📍 {res.Latitude:F5}, {res.Longitude:F5}";
                    LocationCaptured = true;

                    // Bairros já vinculados
                    var existingNames = res.Neighborhoods?.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
                    foreach (var item in Neighborhoods)
                        item.IsSelected = existingNames.Contains(item.Name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar reservatório: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Salvar ─────────────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(ReservoirName))
            {
                IsSuccess = false;
                StatusMessage = "Informe o nome do reservatório.";
                return;
            }

            // Tenta parsear as coordenadas dos campos internos ou dos Entry manuais
            double lat = _latitude, lon = _longitude;
            bool coordsOk = (lat != 0 && lon != 0) ||
                            (double.TryParse(ReservoirLatitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out lat) &&
                             double.TryParse(ReservoirLongitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out lon));

            if (!coordsOk)
            {
                IsSuccess = false;
                StatusMessage = "Capture a localização ou informe as coordenadas.";
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

            var selectedNeighborhoods = Neighborhoods.Where(n => n.IsSelected).Select(n => n.Name).ToList();

            try
            {
                bool success;
                if (IsEditing)
                    success = await _apiService.UpdateReservoirAsync(ReservoirId, ReservoirName, lat, lon, token, selectedNeighborhoods);
                else
                    success = await _apiService.CreateReservoirAsync(ReservoirName, lat, lon, token, selectedNeighborhoods);

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
                    await Shell.Current.GoToAsync("..");
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

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string SanitizeCoordinate(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            bool hasNegative = false, hasDecimal = false;
            int decimalDigits = 0;
            var result = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (c == '-' && !hasNegative && result.Length == 0) { hasNegative = true; result.Append(c); }
                else if ((c == '.' || c == ',') && !hasDecimal) { hasDecimal = true; result.Append('.'); }
                else if (char.IsDigit(c)) { if (hasDecimal) { if (decimalDigits < 6) { result.Append(c); decimalDigits++; } } else result.Append(c); }
            }
            return result.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
