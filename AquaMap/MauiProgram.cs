using AquaMap.Services;   
using AquaMap.ViewModels;  
using AquaMap.Views;  
using Microsoft.Extensions.Logging;

namespace AquaMap
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- NOVO: REGISTRAR SERVIÇOS DE API ---
            builder.Services.AddSingleton(new ApiService(new System.Net.Http.HttpClient()));

            // --- REGISTRAR TELAS E VIEWMODELS ---
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<MapViewModel>();
            builder.Services.AddTransient<MapPage>();

            return builder.Build();
        }
    }
}