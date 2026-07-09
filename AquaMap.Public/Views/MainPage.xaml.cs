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

            var markers = _viewModel.Reservoirs.Select(r =>
            {
                var allNeighborhoods = r.Neighborhoods?.Select(n => n.Name).ToList() ?? new List<string>();
                string nText = string.Empty;
                if (allNeighborhoods.Count > 0)
                {
                    if (allNeighborhoods.Count <= 3)
                        nText = string.Join(", ", allNeighborhoods);
                    else
                        nText = string.Join(", ", allNeighborhoods.Take(3)) + $" e mais {allNeighborhoods.Count - 3}";
                }

                return new
                {
                    id = r.Id,
                    lat = r.Latitude,
                    lng = r.Longitude,
                    name = r.Name ?? "Reservatório",
                    status = GetStatusForReservoir(r),
                    neighborhoods = nText
                };
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
        
        /* GAP 2: Ícones acessíveis com formas diferentes */
        .marker-ok {{
            background-color: #10B981;
            width: 28px; height: 28px;
            border-radius: 50%; /* Círculo */
            border: 3px solid white;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
            display: flex; justify-content: center; align-items: center;
            color: white; font-weight: bold; font-size: 14px;
        }}
        .marker-alert {{
            background-color: transparent;
            width: 0; height: 0;
            border-left: 16px solid transparent;
            border-right: 16px solid transparent;
            border-bottom: 28px solid #EF4444; /* Triângulo */
            position: relative;
            filter: drop-shadow(0 2px 3px rgba(0,0,0,0.3));
        }}
        .marker-alert::after {{
            content: '!';
            color: white; font-weight: bold; font-size: 16px;
            position: absolute; top: 7px; left: -3px;
        }}
        .marker-nodata {{
            background-color: #F59E0B;
            width: 26px; height: 26px;
            border-radius: 4px; /* Quadrado */
            border: 3px solid white;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
            display: flex; justify-content: center; align-items: center;
            color: white; font-weight: bold; font-size: 14px;
        }}
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
            
            // GAP 4: Zonas críticas no mapa por bairro (área de risco)
            if (m.status === 'alert') {{
                L.circle([m.lat, m.lng], {{
                    color: '#EF4444',
                    fillColor: '#EF4444',
                    fillOpacity: 0.15,
                    radius: 800, // 800 metros de raio afetado
                    weight: 1
                }}).addTo(map).bindPopup('<b>Zona de Alerta</b><br>Região possivelmente afetada.');
            }}

            // GAP 2: Marcadores acessíveis (ícones/símbolos em vez de apenas cores)
            var iconClass = 'marker-nodata';
            var iconSymbol = '?';
            
            if (m.status === 'ok') {{ iconClass = 'marker-ok'; iconSymbol = '✓'; }}
            else if (m.status === 'alert') {{ iconClass = 'marker-alert'; iconSymbol = ''; }} // Triângulo desenhado via CSS

            var iconHtml = '<div class=""' + iconClass + '"">' + iconSymbol + '</div>';
            
            var customIcon = L.divIcon({{
                html: iconHtml,
                className: '', // Limpa classes padrão do leaflet
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            }});

            var marker = L.marker([m.lat, m.lng], {{ icon: customIcon }}).addTo(map);
            
            var popUpText = '<b style=""font-size:14px"">' + m.name + '</b>';
            if (m.neighborhoods) {{
                popUpText += '<br><span style=""color:#64748B;font-size:11px"">Bairros: ' + m.neighborhoods + '</span>';
            }}
            popUpText += '<br><br><a href=""aquamap://details/' + m.id + '"" style=""color:#0284C7;font-size:13px;font-weight:bold;text-decoration:none;"">Ver Qualidade da Água &rarr;</a>';

            marker.bindPopup(popUpText);
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

        private string GetStatusForReservoir(Reservoir r)
        {
            if (r.WaterAnalyses == null || !r.WaterAnalyses.Any()) return "nodata";
            var last = r.WaterAnalyses.OrderByDescending(x => x.AnalysisDate).First();
            return last.IsPotable ? "ok" : "alert";
        }
    }
}
