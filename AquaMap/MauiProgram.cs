using AquaMap.Services;   
using AquaMap.ViewModels;  
using AquaMap.Views;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Devices;
using AquaMap.Controls;

#if ANDROID
using Microsoft.Maui.Maps.Handlers;
using Android.Gms.Maps.Model;
#endif

namespace AquaMap
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
#if ANDROID
                .UseMauiMaps()
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            MapPinHandler.Mapper.AppendToMapping("CustomPinColor", (handler, view) =>
            {
                if (view is CustomPin cp)
                {
                    float hue = BitmapDescriptorFactory.HueRed; // Default
                    if (cp.PinColor == Colors.Green) hue = BitmapDescriptorFactory.HueGreen;
                    else if (cp.PinColor == Colors.Gray) hue = BitmapDescriptorFactory.HueYellow;
                    
                    // PlatformView no MapPinHandler do Android é do tipo MarkerOptions
                    var markerOptions = handler.PlatformView as MarkerOptions;
                    if (markerOptions != null)
                    {
                        markerOptions.SetIcon(BitmapDescriptorFactory.DefaultMarker(hue));
                    }
                }
            });
#endif

            // --- CARREGAR ARQUIVOS DE CONFIGURAÇÃO (appsettings.json) ---
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("AquaMap.appsettings.json");
            if (stream != null)
            {
                builder.Configuration.AddJsonStream(stream);
            }

#if DEBUG
            using var devStream = assembly.GetManifestResourceStream("AquaMap.appsettings.Development.json");
            if (devStream != null)
            {
                builder.Configuration.AddJsonStream(devStream);
            }
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- INJEÇÃO DA URL SEM HARDCODING ---
            var apiSettings = builder.Configuration.GetSection("ApiSettings");
            string? baseUrl = apiSettings["BaseUrl"];

#if DEBUG
            // Em desenvolvimento local, puxamos os IPs corretos baseados no emulador/sistema
            baseUrl = DeviceInfo.Platform == DevicePlatform.Android 
                ? apiSettings["BaseUrlAndroid"] 
                : apiSettings["BaseUrlWindows"];
#endif

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new InvalidOperationException("A URL base da API não foi configurada nas configurações do aplicativo (appsettings.json).");
            }

            // --- REGISTRAR SERVIÇOS DE API ---
            builder.Services.AddSingleton(new ApiService(new System.Net.Http.HttpClient { BaseAddress = new System.Uri(baseUrl) }));

            // --- REGISTRAR TELAS E VIEWMODELS ---
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<MapViewModel>();
            builder.Services.AddTransient<MapPage>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<CollectionFormViewModel>();
            builder.Services.AddTransient<CollectionFormPage>();
            builder.Services.AddTransient<ReservoirFormViewModel>();
            builder.Services.AddTransient<ReservoirFormPage>();
            builder.Services.AddTransient<ReservoirDetailViewModel>();
            builder.Services.AddTransient<ReservoirDetailPage>();
            builder.Services.AddTransient<UserListViewModel>();
            builder.Services.AddTransient<UserListPage>();
            builder.Services.AddTransient<UserFormViewModel>();
            builder.Services.AddTransient<UserFormPage>();

            return builder.Build();
        }
    }
}