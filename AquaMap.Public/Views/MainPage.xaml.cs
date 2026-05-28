using AquaMap.Public.ViewModels;
using AquaMap.Domain.Entities;
using System.Text.Json;

namespace AquaMap.Public.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadDataAsync();
            UpdateMap();
        }

        private void UpdateMap()
        {
            var markersJson = JsonSerializer.Serialize(_viewModel.Reservoirs.Select(r => new
            {
                id = r.Id,
                lat = r.Latitude,
                lng = r.Longitude,
                name = r.Name,
                color = GetColorForReservoir(r)
            }));

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body {{ padding: 0; margin: 0; }}
        html, body, #map {{ height: 100%; width: 100vw; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([-20.7636, -41.5333], 13);
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '© OpenStreetMap'
        }}).addTo(map);

        var markers = {markersJson};
        markers.forEach(function(m) {{
            var marker = L.circleMarker([m.lat, m.lng], {{
                color: m.color,
                fillColor: m.color,
                fillOpacity: 0.8,
                radius: 12
            }}).addTo(map);
            
            marker.bindPopup('<b>' + m.name + '</b><br><a href=""https://app.local/details?id=' + m.id + '"">Ver Detalhes</a>');
        }});
    </script>
</body>
</html>";

            ReservoirMapWebView.Source = new HtmlWebViewSource { Html = html };
            ReservoirMapWebView.Navigating += OnWebViewNavigating;
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("https://app.local/details?id="))
            {
                e.Cancel = true;
                if (int.TryParse(e.Url.Split("id=")[1], out int id))
                {
                    await Shell.Current.GoToAsync($"ReservoirDetailPage?Id={id}");
                }
            }
        }

        private string GetColorForReservoir(Reservoir r)
        {
            if (r.WaterAnalyses == null || !r.WaterAnalyses.Any()) return "#F59E0B"; // Orange
            var last = r.WaterAnalyses.OrderByDescending(x => x.AnalysisDate).First();
            if (last.ResidualChlorine >= 0.2 && last.ResidualChlorine <= 2.0 &&
                last.Ph >= 6.0 && last.Ph <= 9.5 &&
                last.Turbidity <= 5.0 && last.EColiAbsent)
            {
                return "#10B981"; // Green
            }
            return "#EF4444"; // Red
        }
    }
}
