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
                var firstPin = _viewModel.MapPins[0];
                double lat = firstPin.Location.Latitude;
                double lon = firstPin.Location.Longitude;
                
                // Bounding box para dar o nível de zoom em volta da cidade
                double offset = 0.02;
                
                string mapUrl = $"https://www.openstreetmap.org/export/embed.html?bbox={lon - offset}%2C{lat - offset}%2C{lon + offset}%2C{lat + offset}&layer=mapnik&marker={lat}%2C{lon}";
                
                ReservoirMapWebView.Source = mapUrl;
            }
            else
            {
                // Se não tiver pinos, mostra o centro de Alegre-ES
                ReservoirMapWebView.Source = "https://www.openstreetmap.org/export/embed.html?bbox=-41.55%2C-20.78%2C-41.51%2C-20.74&layer=mapnik&marker=-20.7636%2C-41.5333";
            }
        }
    }
}
