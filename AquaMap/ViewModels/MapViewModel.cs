using AquaMap.Domain.Entities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;

namespace AquaMap.ViewModels
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private readonly Services.ApiService _apiService;

        public ObservableCollection<Pin> MapPins { get; set; } = new();

        public ICommand LoadMapCommand { get; }

        public MapViewModel(Services.ApiService apiService)
        {
            _apiService = apiService;
            LoadMapCommand = new Command(async () => await LoadMapDataAsync());
        }

        private async Task LoadMapDataAsync()
        {
            var data = await _apiService.GetReservoirsAsync();

            MapPins.Clear();
            foreach (var reservoir in data)
            {
                // Verifica se a coordenada não é 0,0 (padrão)
                if (reservoir.Latitude != 0 && reservoir.Longitude != 0)
                {
                    MapPins.Add(new Pin
                    {
                        Label = reservoir.Name,
                        Address = $"Coordenadas: {reservoir.Latitude:F4}, {reservoir.Longitude:F4}",
                        Type = PinType.Place,
                        Location = new Location(reservoir.Latitude, reservoir.Longitude)
                    });
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
