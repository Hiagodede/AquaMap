using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class MapPage : ContentPage
    {
        private readonly MapViewModel _viewModel;

        public MapPage(MapViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Carrega os Pinos da API
            _viewModel.LoadMapCommand?.Execute(null);
            await Task.Delay(1000); // Aguarda carregamento rápido

            // Monta o mapa via WebView (OpenStreetMap) usando a primeira coordenada encontrada
            if (_viewModel.MapPins.Count > 0)
            {
                var firstReservoir = _viewModel.MapPins[0];
                double lat = firstReservoir.Latitude;
                double lon = firstReservoir.Longitude;
                
                // WebView Logic (Windows)
                double offset = 0.02;
                string latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string minLonStr = (lon - offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string minLatStr = (lat - offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string maxLonStr = (lon + offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string maxLatStr = (lat + offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                string mapUrl = $"https://www.openstreetmap.org/export/embed.html?bbox={minLonStr}%2C{minLatStr}%2C{maxLonStr}%2C{maxLatStr}&layer=mapnik&marker={latStr}%2C{lonStr}";
                ReservoirMapWebView.Source = mapUrl;

#if ANDROID
                // Native Map Logic (Android) - adiciona o mapa nativamente
                AddNativeMap(lat, lon);
#endif
            }
            else
            {
                // Se não tiver pinos, mostra o centro de Alegre-ES
                ReservoirMapWebView.Source = "https://www.openstreetmap.org/export/embed.html?bbox=-41.55%2C-20.78%2C-41.51%2C-20.74&layer=mapnik&marker=-20.7636%2C-41.5333";
#if ANDROID
                AddNativeMap(-20.7636, -41.5333);
#endif
            }
        }

#if ANDROID
        private Microsoft.Maui.Controls.Maps.Map? _nativeMap;

        private void AddNativeMap(double lat, double lon)
        {
            // Esconde o WebView no Android e mostra o mapa nativo
            ReservoirMapWebView.IsVisible = false;

            var grid = (Grid)Content;

            if (_nativeMap == null)
            {
                _nativeMap = new Microsoft.Maui.Controls.Maps.Map
                {
                    ItemsSource = _viewModel.NativePins,
                    ItemTemplate = new DataTemplate(() =>
                    {
                        var pin = new AquaMap.Controls.CustomPin();
                        pin.SetBinding(AquaMap.Controls.CustomPin.LocationProperty, "Location");
                        pin.SetBinding(AquaMap.Controls.CustomPin.AddressProperty, "Address");
                        pin.SetBinding(AquaMap.Controls.CustomPin.LabelProperty, "Label");
                        pin.SetBinding(AquaMap.Controls.CustomPin.TypeProperty, "Type");
                        pin.SetBinding(AquaMap.Controls.CustomPin.PinColorProperty, "PinColor");
                        pin.SetBinding(AquaMap.Controls.CustomPin.ReservoirIdProperty, "ReservoirId");
                        pin.InfoWindowClicked += OnInfoWindowClicked;
                        return pin;
                    })
                };

                // Insere o mapa no início do Grid (atrás dos overlays)
                grid.Children.Insert(0, _nativeMap);
            }

            var mapSpan = new Microsoft.Maui.Maps.MapSpan(
                new Microsoft.Maui.Devices.Sensors.Location(lat, lon), 0.05, 0.05);
            _nativeMap.MoveToRegion(mapSpan);
        }

        private async void OnInfoWindowClicked(object? sender, EventArgs e)
        {
            if (sender is AquaMap.Controls.CustomPin pin)
            {
                await Shell.Current.GoToAsync($"ReservoirDetailPage?ReservoirId={pin.ReservoirId}&ReservoirName={Uri.EscapeDataString(pin.Label)}");
            }
        }
#endif
    }
}
