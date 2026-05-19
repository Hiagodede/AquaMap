using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using AquaMap.Domain.Entities;
using AquaMap.Services;

namespace AquaMap.ViewModels
{
    [QueryProperty(nameof(ReservoirId), "ReservoirId")]
    [QueryProperty(nameof(ReservoirName), "ReservoirName")]
    public class ReservoirDetailViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

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

        public ReservoirDetailViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoadHistoryCommand = new Command(async () => await LoadHistoryAsync());
            EditReservoirCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync($"ReservoirFormPage?ReservoirId={ReservoirId}&ReservoirName={Uri.EscapeDataString(ReservoirName)}");
            });
        }

        private async Task LoadHistoryAsync()
        {
            if (ReservoirId <= 0) return;

            IsBusy = true;
            var data = await _apiService.GetWaterAnalysisHistoryAsync(ReservoirId);
            AnalysisHistory.Clear();
            foreach (var item in data)
            {
                AnalysisHistory.Add(item);
            }
            HasData = AnalysisHistory.Count > 0;
            IsEmpty = !HasData;
            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
