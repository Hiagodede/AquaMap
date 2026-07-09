using AquaMap.Domain.Entities;
using AquaMap.Client.Shared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;

namespace AquaMap.ViewModels
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        public ObservableCollection<Reservoir> MapPins { get; set; } = new();
        public ObservableCollection<AquaMap.Controls.CustomPin> NativePins { get; set; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private int _pinCount;
        public int PinCount
        {
            get => _pinCount;
            set { _pinCount = value; OnPropertyChanged(); }
        }

        public ICommand LoadMapCommand { get; }

        public MapViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoadMapCommand = new Command(async () => await LoadMapDataAsync());
        }

        private async Task LoadMapDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var data = await _apiService.GetReservoirsAsync();

                MapPins.Clear();
                NativePins.Clear();
                foreach (var reservoir in data)
                {
                    // Verifica se a coordenada não é 0,0 (padrão) e se está dentro dos limites da Terra
                    if (reservoir.Latitude != 0 && reservoir.Longitude != 0 &&
                        reservoir.Latitude >= -90 && reservoir.Latitude <= 90 &&
                        reservoir.Longitude >= -180 && reservoir.Longitude <= 180)
                    {
                        MapPins.Add(reservoir);

                        var pinColor = Colors.Gray;
                        if (reservoir.StatusColor == "Green") pinColor = Colors.Green;
                        else if (reservoir.StatusColor == "Red") pinColor = Colors.Red;

                        NativePins.Add(new AquaMap.Controls.CustomPin
                        {
                            ReservoirId = reservoir.Id,
                            Label = reservoir.Name,
                            Address = $"Última Situação: {(reservoir.StatusColor == "Green" ? "Própria" : "Imprópria")}",
                            Type = Microsoft.Maui.Controls.Maps.PinType.Place,
                            Location = new Location(reservoir.Latitude, reservoir.Longitude),
                            PinColor = pinColor
                        });
                    }
                }
                PinCount = MapPins.Count;
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
