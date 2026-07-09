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
using AquaMap.Client.Shared;
using AquaMap.Models;
using Microsoft.Maui.Networking;

namespace AquaMap.ViewModels
{
    public class CollectionFormViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly LocalDatabaseService _localDbService;
        private readonly SyncService _syncService;

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

        // GAP 7 — Campo de Ferro (Fe)
        private string _iron = string.Empty;
        public string Iron
        {
            get => _iron;
            set
            {
                _iron = value;
                OnPropertyChanged();
                SaveDraft();
                _ = ValidateIronAsync();
            }
        }

        private bool _isIronValid = true;
        public bool IsIronValid
        {
            get => _isIronValid;
            set { _isIronValid = value; OnPropertyChanged(); }
        }

        private string _ironWarning = string.Empty;
        public string IronWarning
        {
            get => _ironWarning;
            set { _ironWarning = value; OnPropertyChanged(); }
        }

        // GAP 1 — GPS do ponto de coleta (UX 3 Modos)
        private double? _internalLatitude;
        private double? _internalLongitude;
        private string _collectionLatitude = string.Empty;
        private string _collectionLongitude = string.Empty;
        private bool _isFormattingCoord = false;

        public string CollectionLatitude
        {
            get => _collectionLatitude;
            set
            {
                if (_isFormattingCoord) { _collectionLatitude = value; return; }
                _isFormattingCoord = true;
                _collectionLatitude = SanitizeCoordinate(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
            }
        }

        public string CollectionLongitude
        {
            get => _collectionLongitude;
            set
            {
                if (_isFormattingCoord) { _collectionLongitude = value; return; }
                _isFormattingCoord = true;
                _collectionLongitude = SanitizeCoordinate(value ?? "");
                OnPropertyChanged();
                _isFormattingCoord = false;
                SaveDraft();
            }
        }

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

        private bool _isCapturingLocation;
        public bool IsCapturingLocation
        {
            get => _isCapturingLocation;
            set { _isCapturingLocation = value; OnPropertyChanged(); }
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
        public ICommand CaptureCollectionLocationCommand { get; }
        public ICommand ToggleMapPickerCommand { get; }
        public ICommand ToggleManualInputCommand { get; }

        public CollectionFormViewModel(ApiService apiService, LocalDatabaseService localDbService, SyncService syncService)
        {
            _apiService = apiService;
            _localDbService = localDbService;
            _syncService = syncService;
            SubmitCommand = new Command(async () => await SubmitAnalysisAsync());
            LoadReservoirsCommand = new Command(async () => await LoadReservoirsAsync());
            LogoutCommand = new Command(async () => await LogoutAsync());
            CaptureCollectionLocationCommand = new Command(async () => await CaptureCollectionLocationAsync());
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
            Preferences.Default.Set("Draft_Collection_Iron", Iron);
        }

        private void LoadDraft()
        {
            _residualChlorine = Preferences.Default.Get("Draft_Collection_Chlorine", string.Empty);
            _ph = Preferences.Default.Get("Draft_Collection_Ph", string.Empty);
            _turbidity = Preferences.Default.Get("Draft_Collection_Turbidity", string.Empty);
            _eColiAbsent = Preferences.Default.Get("Draft_Collection_EColiAbsent", true);
            _iron = Preferences.Default.Get("Draft_Collection_Iron", string.Empty);
            _collectionLatitude = Preferences.Default.Get("Draft_Collection_Lat", string.Empty);
            _collectionLongitude = Preferences.Default.Get("Draft_Collection_Lng", string.Empty);

            if (!string.IsNullOrWhiteSpace(_collectionLatitude) && !string.IsNullOrWhiteSpace(_collectionLongitude))
            {
                LocationDisplay = $"📍 {_collectionLatitude}, {_collectionLongitude}";
                LocationCaptured = true;
            }
        }

        private void ClearDraft()
        {
            Preferences.Default.Remove("Draft_Collection_ReservoirId");
            Preferences.Default.Remove("Draft_Collection_Chlorine");
            Preferences.Default.Remove("Draft_Collection_Ph");
            Preferences.Default.Remove("Draft_Collection_Turbidity");
            Preferences.Default.Remove("Draft_Collection_EColiAbsent");
            Preferences.Default.Remove("Draft_Collection_Iron");
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

        // GAP 7 — Validação do Ferro
        private System.Threading.CancellationTokenSource? _ironCts;
        private async Task ValidateIronAsync()
        {
            _ironCts?.Cancel();
            _ironCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(500, _ironCts.Token);
                if (string.IsNullOrWhiteSpace(Iron))
                {
                    IsIronValid = true;
                    IronWarning = string.Empty;
                }
                else if (double.TryParse(Iron.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    IsIronValid = val >= 0.0 && val <= 0.3;
                    IronWarning = IsIronValid ? string.Empty : "Ideal: ≤ 0.3 mg/L (Portaria 888)";
                }
                else
                {
                    IsIronValid = false;
                    IronWarning = "Valor inválido.";
                }
            }
            catch (OperationCanceledException) { }
        }

        // GAP 1 — Captura de GPS do ponto de coleta
        public void SetLocationFromMap(double lat, double lng)
        {
            _internalLatitude = lat;
            _internalLongitude = lng;
            _isFormattingCoord = true;
            CollectionLatitude = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CollectionLongitude = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _isFormattingCoord = false;
            OnPropertyChanged(nameof(CollectionLatitude));
            OnPropertyChanged(nameof(CollectionLongitude));
            LocationDisplay = $"📍 {lat:F5}, {lng:F5}";
            LocationCaptured = true;
            IsMapPickerVisible = false;
            try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
        }

        private async Task CaptureCollectionLocationAsync()
        {
            if (IsCapturingLocation) return;
            IsCapturingLocation = true;
            LocationDisplay = "Buscando localização...";
            LocationCaptured = false;
            try
            {
                var location = await Microsoft.Maui.Devices.Sensors.Geolocation.Default.GetLocationAsync(
                    new Microsoft.Maui.Devices.Sensors.GeolocationRequest(
                        Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Medium,
                        TimeSpan.FromSeconds(10)));
                if (location != null)
                {
                    _internalLatitude = location.Latitude;
                    _internalLongitude = location.Longitude;
                    _isFormattingCoord = true;
                    CollectionLatitude = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    CollectionLongitude = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _isFormattingCoord = false;
                    OnPropertyChanged(nameof(CollectionLatitude));
                    OnPropertyChanged(nameof(CollectionLongitude));
                    LocationDisplay = $"📍 {location.Latitude:F5}, {location.Longitude:F5}";
                    LocationCaptured = true;
                    SaveDraft();
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                }
                else
                {
                    LocationDisplay = "Localização não disponível.";
                }
            }
            catch (Exception)
            {
                LocationDisplay = "Ative o GPS e tente novamente.";
            }
            finally
            {
                IsCapturingLocation = false;
            }
        }

        private async Task LoadReservoirsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // 1. Carrega do banco de dados local primeiro (Offline-First)
                var cached = await _localDbService.GetReservoirsAsync().ConfigureAwait(false);
                if (cached != null && cached.Count > 0)
                {
                    var mappedPoints = cached.Select(c => new Reservoir
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Latitude = c.Latitude,
                        Longitude = c.Longitude
                    }).ToList();

                    Reservoirs.Clear();
                    foreach (var res in mappedPoints)
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

                // 2. Se houver rede, busca na API de forma transparente para atualizar o cache local e a lista
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    var data = await _apiService.GetReservoirsAsync().ConfigureAwait(false);
                    if (data != null)
                    {
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

                        // Salva os reservatórios na base SQLite
                        var localReservoirs = data.Select(r => new LocalReservoir
                        {
                            Id = r.Id,
                            Name = r.Name,
                            Latitude = r.Latitude,
                            Longitude = r.Longitude
                        }).ToList();

                        await _localDbService.SaveReservoirsAsync(localReservoirs).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar reservatórios: {ex.Message}");
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
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
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
                // Parse do Ferro (opcional, padrão 0)
                double.TryParse(Iron?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ironVal);

                // Resolver coordenadas da coleta
                double? finalLat = _internalLatitude, finalLng = _internalLongitude;
                if (!finalLat.HasValue && !string.IsNullOrWhiteSpace(CollectionLatitude) && !string.IsNullOrWhiteSpace(CollectionLongitude))
                {
                    if (double.TryParse(CollectionLatitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double l1) &&
                        double.TryParse(CollectionLongitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double l2))
                    {
                        finalLat = l1;
                        finalLng = l2;
                    }
                }

                // Criar entidade local do banco de dados SQLite com IsPendingSync = true
                var localAnalysis = new LocalWaterAnalysis
                {
                    AnalysisDate = DateTime.UtcNow,
                    ResidualChlorine = chlorine,
                    Ph = phVal,
                    Turbidity = turbidityVal,
                    EColiAbsent = EColiAbsent,
                    Iron = ironVal,
                    CollectionLatitude = finalLat,
                    CollectionLongitude = finalLng,
                    ReservoirId = SelectedReservoir.Id,
                    IsPendingSync = true
                };

                await _localDbService.SaveAnalysisAsync(localAnalysis);

                bool online = Connectivity.NetworkAccess == NetworkAccess.Internet;
                bool syncSuccess = false;

                if (online)
                {
                    // Tenta sincronizar imediatamente em segundo plano
                    syncSuccess = await _syncService.SyncPendingAnalysisAsync();
                }

                // Limpar campos e rascunho
                ClearDraft();
                _residualChlorine = ""; 
                _ph = ""; 
                _turbidity = "";
                _iron = "";
                _selectedReservoir = null;
                _eColiAbsent = true;
                _internalLatitude = null;
                _internalLongitude = null;
                _collectionLatitude = "";
                _collectionLongitude = "";
                LocationDisplay = string.Empty;
                LocationCaptured = false;
                IsMapPickerVisible = false;
                IsManualInputVisible = false;
                OnPropertyChanged(nameof(ResidualChlorine));
                OnPropertyChanged(nameof(Ph));
                OnPropertyChanged(nameof(Turbidity));
                OnPropertyChanged(nameof(Iron));
                OnPropertyChanged(nameof(SelectedReservoir));
                OnPropertyChanged(nameof(EColiAbsent));
                OnPropertyChanged(nameof(CollectionLatitude));
                OnPropertyChanged(nameof(CollectionLongitude));

                // Reset warnings
                IsChlorineValid = true;
                ChlorineWarning = string.Empty;
                IsPhValid = true;
                PhWarning = string.Empty;
                IsTurbidityValid = true;
                TurbidityWarning = string.Empty;
                IsIronValid = true;
                IronWarning = string.Empty;

                IsSuccess = true;
                StatusColor = "Green";
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                if (syncSuccess)
                {
                    StatusMessage = "Coleta registrada e enviada com sucesso!";
                }
                else
                {
                    StatusMessage = online 
                        ? "Coleta registrada localmente! Sincronização em andamento..." 
                        : "Coleta registrada com sucesso! (Salva offline)";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusColor = "Red";
                StatusMessage = $"Erro ao salvar coleta: {ex.Message}";
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
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
