using Microsoft.Extensions.Logging;
using AquaMap.Public.Services;
using AquaMap.Public.ViewModels;
using AquaMap.Public.Views;

namespace AquaMap.Public;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseMauiMaps()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ReservoirDetailViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReservoirDetailPage>();

		return builder.Build();
	}
}
