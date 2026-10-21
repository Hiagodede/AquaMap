using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using AquaMap.Domain.Entities;
using AquaMap.Services;
using AquaMap.Client.Shared;
using AquaMap.Helpers;
using System.Linq;

namespace AquaMap.ViewModels
{
    [QueryProperty(nameof(ReservoirId), "ReservoirId")]
    [QueryProperty(nameof(ReservoirName), "ReservoirName")]
    public class ReservoirDetailViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly LocalDatabaseService _localDbService;

        public ObservableCollection<WaterAnalysis> AnalysisHistory { get; set; } = new();

        private int _reservoirId;
        public int ReservoirId
        {
            get => _reservoirId;
            set { _reservoirId = value; OnPropertyChanged(); _ = LoadHistoryAsync(); }
        }

        private string _reservoirName = string.Empty;
        public string ReservoirName
        {
            get => _reservoirName;
            set { _reservoirName = value; OnPropertyChanged(); }
        }

        private WaterAnalysis? _latestAnalysis;
        public WaterAnalysis? LatestAnalysis
        {
            get => _latestAnalysis;
            set 
            { 
                _latestAnalysis = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasLatestAnalysis));
            }
        }

        public bool HasLatestAnalysis => LatestAnalysis != null;

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

        // Only administrators edit or delete an analysis (the API enforces it; this just hides the buttons).
        private bool _isAdministrator;
        public bool IsAdministrator
        {
            get => _isAdministrator;
            set { _isAdministrator = value; OnPropertyChanged(); }
        }

        public ICommand LoadHistoryCommand { get; }
        public ICommand EditReservoirCommand { get; }
        public ICommand ExportPdfCommand { get; } // GAP 3
        public ICommand EditAnalysisCommand { get; }
        public ICommand DeleteAnalysisCommand { get; }

        public ReservoirDetailViewModel(ApiService apiService, LocalDatabaseService localDbService, PdfExportService pdfExportService)
        {
            _apiService = apiService;
            _localDbService = localDbService;
            
            LoadHistoryCommand = new Command(async () => await LoadHistoryAsync());
            EditAnalysisCommand = new Command<WaterAnalysis>(async analysis => await EditAnalysisAsync(analysis));
            DeleteAnalysisCommand = new Command<WaterAnalysis>(async analysis => await DeleteAnalysisAsync(analysis));
            _ = LoadRoleAsync();
            
            EditReservoirCommand = new Command(async () =>
            {
                if (IsBusy) return;
                IsBusy = true;
                try
                {
                    await Shell.Current.GoToAsync($"ReservoirFormPage?ReservoirId={ReservoirId}&ReservoirName={Uri.EscapeDataString(ReservoirName)}");
                }
                finally
                {
                    IsBusy = false;
                }
            });

            ExportPdfCommand = new Command(async () =>
            {
                if (IsBusy) return;
                if (AnalysisHistory.Count == 0)
                {
                    try { await Shell.Current.DisplayAlert("Exportar PDF", "Não há análises para exportar neste reservatório.", "OK"); }
                    catch (Exception alertEx) { System.Diagnostics.Debug.WriteLine($"Falha ao exibir alerta: {alertEx.Message}"); }
                    return;
                }
                IsBusy = true;
                try
                {
                    var filePath = await pdfExportService.GenerateReservoirReportAsync(ReservoirName, AnalysisHistory);

                    // The share sheet is the confirmation; the internal path means nothing to the technician.
                    await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Boletim de Qualidade da Água",
                        File = new ShareFile(filePath)
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao exportar PDF: {ex}");
                    try
                    {
                        await Shell.Current.DisplayAlert("Erro", "Não foi possível exportar o PDF neste aparelho.", "OK");
                    }
                    catch (Exception alertEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Falha ao exibir alerta: {alertEx.Message}");
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        private async Task LoadHistoryAsync()
        {
            if (ReservoirId <= 0 || IsBusy) return;

            IsBusy = true;
            try
            {
                // 1. Carrega do banco de dados local primeiro (Offline-First)
                var cached = await _localDbService.GetAnalysisHistoryAsync(ReservoirId);
                if (cached != null && cached.Count > 0)
                {
                    var mappedHistory = cached.Select(c => c.ToDomain()).ToList();

                    AnalysisHistory.Clear();
                    WaterAnalysis? latest = null;
                    foreach (var item in mappedHistory)
                    {
                        AnalysisHistory.Add(item);
                        if (latest == null || item.AnalysisDate > latest.AnalysisDate)
                        {
                            latest = item;
                        }
                    }
                    LatestAnalysis = latest;
                    HasData = AnalysisHistory.Count > 0;
                    IsEmpty = !HasData;
                }

                // 2. Busca da API em segundo plano se conectado à internet (Stale-While-Revalidate)
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    var data = await _apiService.GetWaterAnalysisHistoryAsync(ReservoirId);
                    if (data != null)
                    {
                        // Salva os dados baixados no banco de dados local
                        var localList = data.Select(a => Models.LocalWaterAnalysis.FromDomain(a)).ToList();

                        await _localDbService.SaveAnalysisHistoryAsync(ReservoirId, localList);

                        // Recarrega do banco local para obter a mesclagem com dados que porventura ainda estejam pendentes de sincronização
                        var merged = await _localDbService.GetAnalysisHistoryAsync(ReservoirId);
                        if (merged != null)
                        {
                            AnalysisHistory.Clear();
                            WaterAnalysis? latest = null;
                            foreach (var c in merged)
                            {
                                var item = c.ToDomain();
                                AnalysisHistory.Add(item);
                                if (latest == null || item.AnalysisDate > latest.AnalysisDate)
                                {
                                    latest = item;
                                }
                            }
                            LatestAnalysis = latest;
                            HasData = AnalysisHistory.Count > 0;
                            IsEmpty = !HasData;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar histórico: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadRoleAsync()
        {
            try
            {
                IsAdministrator = JwtRole.IsAdministrator(await SecureStorage.Default.GetAsync("jwt_token"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao ler o perfil do usuário: {ex.Message}");
                IsAdministrator = false;
            }
        }

        // A collection still queued on this device has no server id: it can only be changed after it syncs.
        private static async Task<bool> EnsureSyncedAsync(WaterAnalysis analysis)
        {
            if (!analysis.IsPendingSync && analysis.Id > 0) return true;
            await Shell.Current.DisplayAlert("Análise ainda não enviada",
                "Esta coleta ainda está salva só no aparelho. Espere ela ser enviada ao servidor para editar ou excluir.", "OK");
            return false;
        }

        private async Task EditAnalysisAsync(WaterAnalysis? analysis)
        {
            if (analysis is null || !IsAdministrator || IsBusy) return;
            try
            {
                if (!await EnsureSyncedAsync(analysis)) return;
                IsBusy = true;
                await Shell.Current.GoToAsync("AnalysisFormPage", new Dictionary<string, object>
                {
                    ["Analysis"] = analysis,
                    ["ReservoirName"] = ReservoirName
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao abrir edição da análise: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteAnalysisAsync(WaterAnalysis? analysis)
        {
            if (analysis is null || !IsAdministrator || IsBusy) return;
            try
            {
                if (!await EnsureSyncedAsync(analysis)) return;

                if (!await Shell.Current.DisplayAlert("Excluir análise",
                        $"Excluir a análise de {analysis.AnalysisDateLocal:dd/MM/yyyy HH:mm} de '{ReservoirName}'? Essa ação não pode ser desfeita.",
                        "Excluir", "Cancelar"))
                    return;

                var token = await SecureStorage.Default.GetAsync("jwt_token");
                if (string.IsNullOrEmpty(token))
                {
                    await Shell.Current.DisplayAlert("Sessão expirada", "Faça login novamente.", "OK");
                    return;
                }

                IsBusy = true;
                var result = await _apiService.DeleteWaterAnalysisAsync(analysis.Id, token);
                // 404: someone else already deleted it; either way it is gone.
                if (result.Success || result.StatusCode == 404)
                {
                    await _localDbService.DeleteSyncedAnalysisAsync(analysis.Id);
                    AnalysisHistory.Remove(analysis);
                    LatestAnalysis = AnalysisHistory.OrderByDescending(a => a.AnalysisDate).FirstOrDefault();
                    HasData = AnalysisHistory.Count > 0;
                    IsEmpty = !HasData;
                }
                else
                {
                    await Shell.Current.DisplayAlert("Não foi possível excluir", result.StatusCode switch
                    {
                        401 => "Sessão expirada. Faça login novamente.",
                        403 => "Apenas administradores podem excluir análises.",
                        _ => "Erro ao excluir a análise. Verifique a conexão e tente de novo."
                    }, "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao excluir análise: {ex}");
                try { await Shell.Current.DisplayAlert("Erro", "Não foi possível excluir a análise. Verifique a conexão e tente de novo.", "OK"); }
                catch (Exception alertEx) { System.Diagnostics.Debug.WriteLine($"Falha ao exibir alerta: {alertEx.Message}"); }
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
