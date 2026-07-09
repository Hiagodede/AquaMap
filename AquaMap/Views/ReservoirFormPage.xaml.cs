using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class ReservoirFormPage : ContentPage
    {
        private readonly ReservoirFormViewModel _viewModel;
        private bool _mapLoaded = false;

        public ReservoirFormPage(ReservoirFormViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;

            // Observa mudanças no IsMapPickerVisible para carregar/descarregar o mapa
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            MapPickerWebView.Navigating += OnMapPickerNavigating;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.InitializeForm();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // ── Controle do mapa ──────────────────────────────────────────────────
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReservoirFormViewModel.IsMapPickerVisible))
            {
                if (_viewModel.IsMapPickerVisible && !_mapLoaded)
                {
                    LoadMapPicker();
                    _mapLoaded = true;
                }
            }
        }

        private void LoadMapPicker()
        {
            // Centro em Alegre/ES — ajuste conforme necessário
            const double centerLat = -20.7636;
            const double centerLng = -41.5333;

            var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body, html {{ height: 100%; width: 100%; overflow: hidden; }}
        #map {{ height: 100vh; width: 100vw; cursor: crosshair; }}
        #tip {{
            position: absolute; top: 10px; left: 50%; transform: translateX(-50%);
            background: rgba(15,23,42,0.85); color: white;
            padding: 8px 16px; border-radius: 20px;
            font-family: sans-serif; font-size: 13px; z-index: 999;
            pointer-events: none;
        }}
    </style>
</head>
<body>
    <div id='tip'>🎯 Toque no mapa para marcar a localização</div>
    <div id='map'></div>
    <script>
        var marker = null;
        var map = L.map('map', {{ zoomControl: true }}).setView([{centerLat}, {centerLng}], 14);

        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap'
        }}).addTo(map);

        map.on('click', function(e) {{
            var lat = e.latlng.lat.toFixed(6);
            var lng = e.latlng.lng.toFixed(6);

            // Remove marcador anterior
            if (marker) {{ map.removeLayer(marker); }}

            // Adiciona marcador no ponto clicado
            marker = L.marker([lat, lng], {{
                icon: L.divIcon({{
                    html: '<div style=""background:#0891B2;width:18px;height:18px;border-radius:50%;border:3px solid white;box-shadow:0 2px 8px rgba(0,0,0,0.4)""></div>',
                    className: '',
                    iconSize: [18, 18],
                    iconAnchor: [9, 9]
                }})
            }}).addTo(map);
            marker.bindPopup('<b>📍 Localização selecionada</b><br>Lat: ' + lat + '<br>Lng: ' + lng).openPopup();

            // Comunicar ao MAUI via esquema de URL customizado
            window.location.href = 'aquamap://setlocation/' + lat + '/' + lng;
        }});
    </script>
</body>
</html>";

            MapPickerWebView.Source = new HtmlWebViewSource { Html = html };
        }

        private void OnMapPickerNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("aquamap://setlocation/"))
            {
                e.Cancel = true;
                var parts = e.Url.Replace("aquamap://setlocation/", "").Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
                {
                    _viewModel.SetLocationFromMap(lat, lng);
                }
            }
        }
    }
}
