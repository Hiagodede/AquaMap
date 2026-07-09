using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Networking;
using AquaMap.Domain.Entities;
using AquaMap.Services;
using AquaMap.Client.Shared;
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

        public ICommand LoadHistoryCommand { get; }
        public ICommand EditReservoirCommand { get; }
        public ICommand ExportPdfCommand { get; } // GAP 3

        public ReservoirDetailViewModel(ApiService apiService, LocalDatabaseService localDbService, PdfExportService pdfExportService)
        {
            _apiService = apiService;
            _localDbService = localDbService;
            
            LoadHistoryCommand = new Command(async () => await LoadHistoryAsync());
            
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
                if (IsBusy || AnalysisHistory.Count == 0) return;
                IsBusy = true;
                try
                {
                    var filePath = pdfExportService.GenerateReservoirReport(ReservoirName, AnalysisHistory);
                    await Shell.Current.DisplayAlert("Boletim Gerado", $"PDF salvo em:\n{filePath}", "OK");
                    
                    // Share the file
                    await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Boletim de Qualidade da Água",
                        File = new ShareFile(filePath)
                    });
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Erro", $"Falha ao exportar PDF: {ex.Message}", "OK");
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
                var cached = await _localDbService.GetAnalysisHistoryAsync(ReservoirId).ConfigureAwait(false);
                if (cached != null && cached.Count > 0)
                {
                    var mappedHistory = cached.Select(c => new WaterAnalysis
                    {
                        Id = c.Id,
                        AnalysisDate = c.AnalysisDate,
                        ResidualChlorine = c.ResidualChlorine,
                        Ph = c.Ph,
                        Turbidity = c.Turbidity,
                        EColiAbsent = c.EColiAbsent,
                        Iron = c.Iron,
                        CollectionLatitude = c.CollectionLatitude,
                        CollectionLongitude = c.CollectionLongitude,
                        ReservoirId = c.ReservoirId,
                        IsPendingSync = c.IsPendingSync
                    }).ToList();

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
                    var data = await _apiService.GetWaterAnalysisHistoryAsync(ReservoirId).ConfigureAwait(false);
                    if (data != null)
                    {
                        // Salva os dados baixados no banco de dados local
                        var localList = data.Select(a => new Models.LocalWaterAnalysis
                        {
                            Id = a.Id,
                            AnalysisDate = a.AnalysisDate,
                            ResidualChlorine = a.ResidualChlorine,
                            Ph = a.Ph,
                            Turbidity = a.Turbidity,
                            EColiAbsent = a.EColiAbsent,
                            Iron = a.Iron,
                            CollectionLatitude = a.CollectionLatitude,
                            CollectionLongitude = a.CollectionLongitude,
                            ReservoirId = a.ReservoirId,
                            IsPendingSync = false
                        }).ToList();

                        await _localDbService.SaveAnalysisHistoryAsync(ReservoirId, localList).ConfigureAwait(false);

                        // Recarrega do banco local para obter a mesclagem com dados que porventura ainda estejam pendentes de sincronização
                        var merged = await _localDbService.GetAnalysisHistoryAsync(ReservoirId).ConfigureAwait(false);
                        if (merged != null)
                        {
                            AnalysisHistory.Clear();
                            WaterAnalysis? latest = null;
                            foreach (var c in merged)
                            {
                                var item = new WaterAnalysis
                                {
                                    Id = c.Id,
                                    AnalysisDate = c.AnalysisDate,
                                    ResidualChlorine = c.ResidualChlorine,
                                    Ph = c.Ph,
                                    Turbidity = c.Turbidity,
                                    EColiAbsent = c.EColiAbsent,
                                    Iron = c.Iron,
                                    CollectionLatitude = c.CollectionLatitude,
                                    CollectionLongitude = c.CollectionLongitude,
                                    ReservoirId = c.ReservoirId,
                                    IsPendingSync = c.IsPendingSync
                                };
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
