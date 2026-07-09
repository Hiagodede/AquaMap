using Microsoft.Extensions.Logging;
using AquaMap.Client.Shared;
using AquaMap.Public.ViewModels;
using AquaMap.Public.Views;
using System.Reflection;

namespace AquaMap.Public;

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

		builder.Services.AddSingleton(new ApiService(ApiClientFactory.Create(Assembly.GetExecutingAssembly())));
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ReservoirDetailViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReservoirDetailPage>();

		return builder.Build();
	}
}
