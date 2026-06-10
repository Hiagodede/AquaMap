using AquaMap.Public.ViewModels;
using AquaMap.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AquaMap.Public.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;
        private bool _mapRendered = false;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
            ReservoirMapWebView.Navigating += OnWebViewNavigating;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_mapRendered) return; // Evitar re-render ao voltar da tela de detalhes
            
            await _viewModel.LoadDataAsync();
            RenderMap();
            _mapRendered = true;
        }

        private void RenderMap()
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var markers = _viewModel.Reservoirs.Select(r => new
            {
                id = r.Id,
                lat = r.Latitude,
                lng = r.Longitude,
                name = r.Name ?? "Reservatório",
                color = GetColorForReservoir(r)
            }).ToList();

            var markersJson = JsonSerializer.Serialize(markers, options);

            var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        * {{ box-sizing: border-box; }}
        body, html {{ padding: 0; margin: 0; height: 100%; width: 100%; overflow: hidden; }}
        #map {{ height: 100vh; width: 100vw; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map', {{ zoomControl: true }}).setView([-20.7636, -41.5333], 13);
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap'
        }}).addTo(map);

        var markers = {markersJson};
        markers.forEach(function(m) {{
            var circleMarker = L.circleMarker([m.lat, m.lng], {{
                color: m.color,
                fillColor: m.color,
                fillOpacity: 0.85,
                radius: 14,
                weight: 2
            }}).addTo(map);
            circleMarker.bindPopup(
                '<b style=""font-size:14px"">' + m.name + '</b><br>' +
                '<a href=""aquamap://details/' + m.id + '"" style=""color:#0284C7;font-size:12px"">Ver Detalhes &rarr;</a>'
            );
            circleMarker.on('click', function() {{ circleMarker.openPopup(); }});
        }});
    </script>
</body>
</html>";

            ReservoirMapWebView.Source = new HtmlWebViewSource { Html = html };
        }

        private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("aquamap://details/"))
            {
                e.Cancel = true;
                var idStr = e.Url.Replace("aquamap://details/", "");
                if (int.TryParse(idStr, out int id))
                {
                    await Shell.Current.GoToAsync($"ReservoirDetailPage?Id={id}");
                }
            }
        }

        private string GetColorForReservoir(Reservoir r)
        {
            if (r.WaterAnalyses == null || !r.WaterAnalyses.Any()) return "#F59E0B";
            var last = r.WaterAnalyses.OrderByDescending(x => x.AnalysisDate).First();
            bool isGood = last.ResidualChlorine >= 0.2 && last.ResidualChlorine <= 2.0 &&
                          last.Ph >= 6.0 && last.Ph <= 9.5 &&
                          last.Turbidity <= 5.0 && last.EColiAbsent;
            return isGood ? "#10B981" : "#EF4444";
        }
    }
}
