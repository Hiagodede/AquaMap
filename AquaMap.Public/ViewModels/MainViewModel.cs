using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AquaMap.Domain.Entities;
using AquaMap.Public.Services;

namespace AquaMap.Public.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        public ObservableCollection<Reservoir> Reservoirs { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadDataCommand { get; }
        public ICommand PinClickedCommand { get; }

        public MainViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoadDataCommand = new Command(async () => await LoadDataAsync());
            PinClickedCommand = new Command<Reservoir>(async (r) => await GoToDetailsAsync(r));
        }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var data = await _apiService.GetReservoirsAsync();
                Reservoirs.Clear();
                foreach (var r in data)
                {
                    Reservoirs.Add(r);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GoToDetailsAsync(Reservoir reservoir)
        {
            if (reservoir == null) return;
            await Shell.Current.GoToAsync($"ReservoirDetailPage?Id={reservoir.Id}");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
