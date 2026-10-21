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
using AquaMap.Client.Shared;
using AquaMap.Helpers;

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
        private readonly LocalDatabaseService _localDbService;

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
            set { _reservoirId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(PageTitle)); OnPropertyChanged(nameof(CanDelete)); }
        }

        private string _reservoirName = string.Empty;
        public string ReservoirName
        {
            get => _reservoirName;
            set { _reservoirName = value; OnPropertyChanged(); SaveDraft(); }
        }

        // ── Coordenadas ────────────────────────────────────────────────────────
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
                _reservoirLatitude = CoordinateInput.Sanitize(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
                RefreshLocationFromFields();
            }
        }

        public string ReservoirLongitude
        {
            get => _reservoirLongitude;
            set
            {
                if (_isFormattingCoord) { _reservoirLongitude = value; return; }
                _isFormattingCoord = true;
                _reservoirLongitude = CoordinateInput.Sanitize(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
                RefreshLocationFromFields();
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

        // Only administrators delete (the API enforces it; this just hides the button for technicians).
        private bool _isAdministrator;
        public bool CanDelete => IsEditing && _isAdministrator;

        // Number of analyses deleted along with the reservoir, shown in the confirmation.
        private int _analysisCount;

        // ── Comandos ───────────────────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CaptureLocationCommand { get; }
        public ICommand ToggleMapPickerCommand { get; }
        public ICommand ToggleManualInputCommand { get; }

        public ReservoirFormViewModel(ApiService apiService, LocalDatabaseService localDbService)
        {
            _apiService = apiService;
            _localDbService = localDbService;

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

        private bool _initialized;
        private bool _loadFailed;

        // Runs once per page instance: OnAppearing fires again after dialogs, child pages and tab switches,
        // and reloading then overwrote unsaved edits (neighborhoods were unchecked).
        public void InitializeForm()
        {
            if (_initialized) return;
            _initialized = true;

            if (IsEditing)
            {
                _ = LoadRoleAsync();
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
            _isFormattingCoord = true;
            ReservoirLatitude = CoordinateInput.Format(lat);
            ReservoirLongitude = CoordinateInput.Format(lng);
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
                    _isFormattingCoord = true;
                    ReservoirLatitude = CoordinateInput.Format(location.Latitude);
                    ReservoirLongitude = CoordinateInput.Format(location.Longitude);
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

        private async Task LoadRoleAsync()
        {
            try
            {
                _isAdministrator = JwtRole.IsAdministrator(await SecureStorage.Default.GetAsync("jwt_token"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao ler o perfil do usuário: {ex.Message}");
                _isAdministrator = false;
            }
            OnPropertyChanged(nameof(CanDelete));
        }

        private async Task LoadReservoirDataAsync()
        {
            if (ReservoirId <= 0) return;
            IsBusy = true;
            try
            {
                var res = await _apiService.GetReservoirByIdAsync(ReservoirId);
                if (res == null)
                {
                    _loadFailed = true;
                    StatusMessage = "Não foi possível carregar os dados do reservatório. Verifique a conexão e abra a edição de novo.";
                }
                else
                {
                    // Coordenadas
                    _isFormattingCoord = true;
                    ReservoirLatitude = CoordinateInput.Format(res.Latitude);
                    ReservoirLongitude = CoordinateInput.Format(res.Longitude);
                    _isFormattingCoord = false;
                    OnPropertyChanged(nameof(ReservoirLatitude));
                    OnPropertyChanged(nameof(ReservoirLongitude));
                    LocationDisplay = $"📍 {res.Latitude:F5}, {res.Longitude:F5}";
                    LocationCaptured = true;

                    // Bairros já vinculados
                    var existingNames = res.Neighborhoods?.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
                    foreach (var item in Neighborhoods)
                        item.IsSelected = existingNames.Contains(item.Name);

                    _analysisCount = res.WaterAnalyses?.Count ?? 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar reservatório: {ex.Message}");
                _loadFailed = true;
                StatusMessage = "Não foi possível carregar os dados do reservatório. Verifique a conexão e abra a edição de novo.";
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

            // Saving an edit whose data never loaded would send empty neighborhoods and erase them on the server.
            if (_loadFailed)
            {
                IsSuccess = false;
                StatusMessage = "Os dados do reservatório não foram carregados. Volte e abra a edição de novo.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ReservoirName))
            {
                IsSuccess = false;
                StatusMessage = "Informe o nome do reservatório.";
                return;
            }

            // The fields hold GPS/map values and any manual edit made afterwards (QA EST-12).
            var coords = CoordinateInput.Parse(ReservoirLatitude, ReservoirLongitude);
            if (coords.Status != CoordinateInputStatus.Valid)
            {
                IsSuccess = false;
                StatusMessage = coords.Status == CoordinateInputStatus.Empty
                    ? "Capture a localização ou informe as coordenadas."
                    : "Coordenadas inválidas: a latitude deve estar entre -90 e 90 e a longitude entre -180 e 180.";
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
                return;
            }
            double lat = coords.Latitude, lon = coords.Longitude;

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
            if (!CanDelete || IsBusy) return;

            // Without the loaded data the confirmation cannot say how many analyses will be erased.
            if (_loadFailed)
            {
                IsSuccess = false;
                StatusMessage = "Os dados do reservatório não foram carregados. Volte e abra a edição de novo.";
                return;
            }

            try
            {
                if (_analysisCount == 0)
                {
                    if (!await Shell.Current.DisplayAlert("Excluir reservatório",
                            $"Excluir '{ReservoirName}'? Essa ação não pode ser desfeita.", "Excluir", "Cancelar"))
                        return;
                }
                else
                {
                    var analyses = _analysisCount == 1 ? "1 análise registrada" : $"{_analysisCount} análises registradas";
                    if (!await Shell.Current.DisplayAlert("Excluir reservatório e análises",
                            $"Excluir '{ReservoirName}' apaga também {analyses} dele. O histórico de qualidade da água desse reservatório será perdido.",
                            "Continuar", "Cancelar"))
                        return;
                    if (!await Shell.Current.DisplayAlert("Tem certeza?",
                            $"'{ReservoirName}' e {analyses} serão apagados definitivamente. Essa ação não pode ser desfeita.",
                            "Excluir tudo", "Cancelar"))
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
                var result = await _apiService.DeleteReservoirAsync(ReservoirId, _analysisCount, token);
                // 404: someone else already deleted it; either way it is gone.
                if (result.Success || result.StatusCode == 404)
                {
                    await _localDbService.DeleteReservoirAsync(ReservoirId);
                    // Back to the map: the detail page underneath shows the reservoir that no longer exists.
                    await Shell.Current.GoToAsync("../..");
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = result.StatusCode switch
                    {
                        401 => "Sessão expirada. Faça login novamente.",
                        403 => "Apenas administradores podem excluir reservatórios.",
                        409 => "Novas análises foram registradas neste reservatório. Nada foi excluído. Volte e abra a edição de novo para conferir.",
                        _ => "Erro ao excluir reservatório. Verifique a conexão e tente de novo."
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao excluir reservatório: {ex.Message}");
                IsSuccess = false;
                StatusMessage = "Não foi possível excluir o reservatório. Verifique a conexão e tente de novo.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // A manual edit after GPS/map must update the card: it shows what will be saved, or nothing.
        private void RefreshLocationFromFields()
        {
            var coords = CoordinateInput.Parse(ReservoirLatitude, ReservoirLongitude);
            LocationCaptured = coords.Status == CoordinateInputStatus.Valid;
            LocationDisplay = LocationCaptured ? $"📍 {coords.Latitude:F5}, {coords.Longitude:F5}" : string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
