using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AquaMap.Domain.Entities;
using AquaMap.Public.Services;

namespace AquaMap.Public.ViewModels
{
    [QueryProperty(nameof(ReservoirId), "Id")]
    public class ReservoirDetailViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private int _reservoirId;
        public int ReservoirId
        {
            get => _reservoirId;
            set { _reservoirId = value; _ = LoadDataAsync(); }
        }

        private Reservoir? _reservoir;
        public Reservoir? Reservoir
        {
            get => _reservoir;
            set { _reservoir = value; OnPropertyChanged(); }
        }

        public ObservableCollection<WaterAnalysis> Analyses { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ReservoirDetailViewModel(ApiService apiService)
        {
            _apiService = apiService;
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy || ReservoirId == 0) return;
            IsBusy = true;
            try
            {
                Reservoir = await _apiService.GetReservoirByIdAsync(ReservoirId);
                Analyses.Clear();
                if (Reservoir?.WaterAnalyses != null)
                {
                    foreach (var a in Reservoir.WaterAnalyses.OrderByDescending(x => x.AnalysisDate))
                    {
                        Analyses.Add(a);
                    }
                }
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
