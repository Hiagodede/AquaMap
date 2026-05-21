using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Networking;
using AquaMap.Domain.Entities;
using AquaMap.Domain.Interfaces;
using System.Linq;

namespace AquaMap.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Services.ApiService _apiService;
        private readonly Services.LocalDatabaseService _localDbService;
        private System.Collections.Generic.List<Reservoir> _allPoints = new();
        private string _searchText = string.Empty;

        // Lista que a tela vai "vigiar" para mostrar os itens
        public ObservableCollection<Reservoir> Points { get; set; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterPoints();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasData)); }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        public bool IsEmpty => !IsBusy && Points.Count == 0;
        public bool HasData => !IsBusy && Points.Count > 0;

        public ICommand LoadPointsCommand { get; }
        public ICommand OpenDetailCommand { get; }
        public ICommand AddReservoirCommand { get; }

        public MainViewModel(Services.ApiService apiService, Services.LocalDatabaseService localDbService)
        {
            _apiService = apiService;
            _localDbService = localDbService;

            // Configura o comando para carregar os dados
            LoadPointsCommand = new Command(async () => await LoadDataAsync());
            OpenDetailCommand = new Command<Reservoir>(async (r) =>
            {
                if (r == null || IsBusy) return;
                IsBusy = true;
                try
                {
                    await Shell.Current.GoToAsync($"ReservoirDetailPage?ReservoirId={r.Id}&ReservoirName={Uri.EscapeDataString(r.Name)}");
                }
                finally
                {
                    IsBusy = false;
                }
            });
            AddReservoirCommand = new Command(async () =>
            {
                if (IsBusy) return;
                IsBusy = true;
                try
                {
                    await Shell.Current.GoToAsync("ReservoirFormPage");
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        private async Task LoadDataAsync()
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

                    // Carrega as análises do banco local para computar o StatusColor
                    foreach (var r in mappedPoints)
                    {
                        var localAnalyses = await _localDbService.GetAnalysisHistoryAsync(r.Id).ConfigureAwait(false);
                        if (localAnalyses != null && localAnalyses.Count > 0)
                        {
                            r.WaterAnalyses = localAnalyses.Select(a => new WaterAnalysis
                            {
                                Id = a.Id,
                                AnalysisDate = a.AnalysisDate,
                                ResidualChlorine = a.ResidualChlorine,
                                Ph = a.Ph,
                                Turbidity = a.Turbidity,
                                EColiAbsent = a.EColiAbsent,
                                ReservoirId = a.ReservoirId
                            }).ToList();
                        }
                    }

                    _allPoints = mappedPoints;
                    FilterPoints();
                }

                // 2. Se houver rede, busca na API de forma transparente para atualizar o cache local e a tela
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    var data = await _apiService.GetReservoirsAsync().ConfigureAwait(false);
                    if (data != null)
                    {
                        _allPoints = data;
                        FilterPoints();

                        // Salva os reservatórios na base SQLite
                        var localReservoirs = data.Select(r => new Models.LocalReservoir
                        {
                            Id = r.Id,
                            Name = r.Name,
                            Latitude = r.Latitude,
                            Longitude = r.Longitude
                        }).ToList();

                        await _localDbService.SaveReservoirsAsync(localReservoirs).ConfigureAwait(false);

                        // Cacheia o histórico de análises de cada um deles localmente
                        foreach (var r in data)
                        {
                            if (r.WaterAnalyses != null && r.WaterAnalyses.Count > 0)
                            {
                                var localAnalyses = r.WaterAnalyses.Select(a => new Models.LocalWaterAnalysis
                                {
                                    Id = a.Id,
                                    AnalysisDate = a.AnalysisDate,
                                    ResidualChlorine = a.ResidualChlorine,
                                    Ph = a.Ph,
                                    Turbidity = a.Turbidity,
                                    EColiAbsent = a.EColiAbsent,
                                    ReservoirId = a.ReservoirId,
                                    IsPendingSync = false
                                }).ToList();

                                await _localDbService.SaveAnalysisHistoryAsync(r.Id, localAnalyses).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar dados: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasData));
            }
        }

        private void FilterPoints()
        {
            Points.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allPoints
                : _allPoints.Where(p => p.Name != null && p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var p in filtered)
            {
                Points.Add(p);
            }
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasData));
        }

        // Boilerplate do MVVM (padrão para avisar a tela que algo mudou)
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}