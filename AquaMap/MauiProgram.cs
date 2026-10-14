using AquaMap.Services;
using AquaMap.ViewModels;
using AquaMap.Views;
using Microsoft.Extensions.Logging;
using System.Reflection;
using AquaMap.Client.Shared;
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

            // On pt-BR devices Android's numeric key listener only accepts ',' as the decimal
            // separator, so typing '.' is silently blocked. Accept both (the view models parse
            // either) plus '-' for coordinates. SetRawInputType keeps the numeric keyboard.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.Keyboard), (handler, view) =>
            {
                if (view is Entry entry && entry.Keyboard == Keyboard.Numeric)
                {
                    handler.PlatformView.KeyListener = Android.Text.Method.DigitsKeyListener.GetInstance("0123456789.,-");
                    handler.PlatformView.SetRawInputType(
                        Android.Text.InputTypes.ClassNumber |
                        Android.Text.InputTypes.NumberFlagDecimal |
                        Android.Text.InputTypes.NumberFlagSigned);
                }
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- REGISTRAR SERVIÇOS DE API E LOCAIS ---
            builder.Services.AddSingleton(new ApiService(ApiClientFactory.Create(Assembly.GetExecutingAssembly())));
            builder.Services.AddSingleton<LocalDatabaseService>();
            builder.Services.AddSingleton<SyncService>();
            builder.Services.AddSingleton<PdfExportService>();

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