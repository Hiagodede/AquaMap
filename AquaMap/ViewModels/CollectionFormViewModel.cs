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

        // null = not informed yet. No default: a forgotten field must not count as "absent".
        private bool? _eColiAbsent;
        public bool? EColiAbsent
        {
            get => _eColiAbsent;
            set
            {
                _eColiAbsent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEColiAbsentSelected));
                OnPropertyChanged(nameof(IsEColiPresentSelected));
                SaveDraft();
            }
        }

        public bool IsEColiAbsentSelected
        {
            get => _eColiAbsent == true;
            set { if (value) EColiAbsent = true; }
        }

        public bool IsEColiPresentSelected
        {
            get => _eColiAbsent == false;
            set { if (value) EColiAbsent = false; }
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
            Preferences.Default.Set("Draft_Collection_EColi", EColiAbsent switch { true => 1, false => 0, null => -1 });
            Preferences.Default.Set("Draft_Collection_Iron", Iron);
        }

        private void LoadDraft()
        {
            _residualChlorine = Preferences.Default.Get("Draft_Collection_Chlorine", string.Empty);
            _ph = Preferences.Default.Get("Draft_Collection_Ph", string.Empty);
            _turbidity = Preferences.Default.Get("Draft_Collection_Turbidity", string.Empty);
            // The old boolean draft key defaulted to "absent"; drop it so it is never restored.
            Preferences.Default.Remove("Draft_Collection_EColiAbsent");
            _eColiAbsent = Preferences.Default.Get("Draft_Collection_EColi", -1) switch { 1 => true, 0 => false, _ => null };
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
            Preferences.Default.Remove("Draft_Collection_EColi");
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
                else if (WaterAnalysisInput.TryParseReading(ResidualChlorine, out double val))
                {
                    ChlorineWarning = WaterAnalysisInput.DescribeChlorine(val) ?? string.Empty;
                    IsChlorineValid = ChlorineWarning.Length == 0;
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
                else if (WaterAnalysisInput.TryParseReading(Ph, out double val))
                {
                    PhWarning = WaterAnalysisInput.DescribePh(val) ?? string.Empty;
                    IsPhValid = PhWarning.Length == 0;
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
                else if (WaterAnalysisInput.TryParseReading(Turbidity, out double val))
                {
                    TurbidityWarning = WaterAnalysisInput.DescribeTurbidity(val) ?? string.Empty;
                    IsTurbidityValid = TurbidityWarning.Length == 0;
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
                else if (WaterAnalysisInput.TryParseReading(Iron, out double val))
                {
                    IronWarning = WaterAnalysisInput.DescribeIron(val) ?? string.Empty;
                    IsIronValid = IronWarning.Length == 0;
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
                var cached = await _localDbService.GetReservoirsAsync();
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
                    var data = await _apiService.GetReservoirsAsync();
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

                        await _localDbService.SaveReservoirsAsync(localReservoirs);
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

        private bool _isConfirming;

        private async Task SubmitAnalysisAsync()
        {
            if (IsBusy) return;

            if (SelectedReservoir == null)
            {
                IsSuccess = false;
                StatusMessage = "Selecione um reservatório.";
                return;
            }

            if (_isConfirming) return;

            // Validated synchronously from the current text, not from the debounced warning flags.
            var input = WaterAnalysisInput.Validate(ResidualChlorine, Ph, Turbidity, Iron, EColiAbsent);
            if (!input.IsValid)
            {
                IsSuccess = false;
                StatusMessage = input.Error!;
                try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress); } catch { }
                return;
            }

            // Out-of-standard readings are the alerts this system exists to record: confirm, never block.
            if (input.OutOfStandard.Count > 0)
            {
                _isConfirming = true;
                bool confirmed;
                try
                {
                    confirmed = await Shell.Current.DisplayAlert(
                        "Parâmetros fora do padrão",
                        "Fora da Portaria 888:\n• " + string.Join("\n• ", input.OutOfStandard) +
                        "\n\nA coleta será registrada e o reservatório ficará em alerta. Os valores estão corretos?",
                        "Registrar", "Revisar");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao confirmar parâmetros: {ex}");
                    IsSuccess = false;
                    StatusMessage = "Não foi possível confirmar os parâmetros. Tente enviar novamente.";
                    confirmed = false;
                }
                finally
                {
                    _isConfirming = false;
                }
                if (!confirmed) return;
            }

            var reading = input.Analysis!;

            // Checked before IsBusy: LogoutAsync is a no-op while busy, which used to leave the form
            // stuck "busy" forever instead of returning to the login screen.
            string? token;
            try
            {
                token = await SecureStorage.Default.GetAsync("jwt_token");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao ler token: {ex}");
                token = null;
            }
            if (string.IsNullOrEmpty(token))
            {
                IsSuccess = false;
                StatusMessage = "Sessão expirada. Faça login novamente.";
                await LogoutAsync();
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // Resolver coordenadas da coleta
                double? finalLat = _internalLatitude, finalLng = _internalLongitude;
                if (!finalLat.HasValue && !string.IsNullOrWhiteSpace(CollectionLatitude) && !string.IsNullOrWhiteSpace(CollectionLongitude))
                {
                    if (double.TryParse(CollectionLatitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double l1) &&
                        double.TryParse(CollectionLongitude.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double l2))
                    {
                        finalLat = l1;
                        finalLng = l2;
                    }
                }

                // Same ranges the API enforces: otherwise the collection would be saved, then rejected on sync.
                if (finalLat is < -90 or > 90 || finalLng is < -180 or > 180)
                {
                    IsSuccess = false;
                    StatusMessage = "Coordenadas inválidas: a latitude deve estar entre -90 e 90 e a longitude entre -180 e 180.";
                    return;
                }

                // Criar entidade local do banco de dados SQLite com IsPendingSync = true
                var localAnalysis = new LocalWaterAnalysis
                {
                    AnalysisDate = DateTime.UtcNow,
                    ResidualChlorine = reading.ResidualChlorine,
                    Ph = reading.Ph,
                    Turbidity = reading.Turbidity,
                    EColiAbsent = reading.EColiAbsent,
                    Iron = reading.Iron,
                    CollectionLatitude = finalLat,
                    CollectionLongitude = finalLng,
                    ReservoirId = SelectedReservoir.Id,
                    IsPendingSync = true
                };

                await _localDbService.SaveAnalysisAsync(localAnalysis);

                bool online = Connectivity.NetworkAccess == NetworkAccess.Internet;
                SyncReport? report = null;
                bool sent = false;

                if (online)
                {
                    report = await _syncService.SyncPendingAnalysisAsync(interactive: true);
                    // The message reflects this collection, not the state of the whole queue.
                    sent = await _localDbService.IsAnalysisSyncedAsync(localAnalysis.LocalId);
                }

                // Limpar campos e rascunho
                ClearDraft();
                _residualChlorine = ""; 
                _ph = ""; 
                _turbidity = "";
                _iron = "";
                _selectedReservoir = null;
                _eColiAbsent = null;
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
                OnPropertyChanged(nameof(IsEColiAbsentSelected));
                OnPropertyChanged(nameof(IsEColiPresentSelected));
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
                if (sent)
                {
                    StatusMessage = "Coleta registrada e enviada com sucesso!";
                }
                else if (report?.SessionExpired == true)
                {
                    StatusMessage = "Coleta salva no aparelho. Sua sessão expirou: saia e entre de novo para enviá-la.";
                }
                else
                {
                    StatusMessage = online
                        ? "Coleta salva no aparelho. O envio será tentado de novo automaticamente."
                        : "Coleta registrada com sucesso! (Salva offline)";
                }

                if (report is { Rejected.Count: > 0 })
                {
                    IsSuccess = false;
                    StatusColor = "Red";
                    StatusMessage += "\n\nColetas recusadas pelo servidor e removidas do aparelho. Registre de novo, corrigindo os valores:\n• "
                        + string.Join("\n• ", report.Rejected);
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
