using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AquaMap.Public.Services;
using AquaMap.Public.ViewModels;
using AquaMap.Public.Views;
using System.Reflection;
using Microsoft.Maui.Devices;

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

		// --- CARREGAR ARQUIVOS DE CONFIGURAÇÃO (appsettings.json) ---
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream("AquaMap.Public.appsettings.json");
		if (stream != null)
		{
			builder.Configuration.AddJsonStream(stream);
		}

#if DEBUG
		using var devStream = assembly.GetManifestResourceStream("AquaMap.Public.appsettings.Development.json");
		if (devStream != null)
		{
			builder.Configuration.AddJsonStream(devStream);
		}
#endif

		// --- INJEÇÃO DA URL SEM HARDCODING ---
		var apiSettings = builder.Configuration.GetSection("ApiSettings");
		string? baseUrl = apiSettings["BaseUrl"];

#if DEBUG
		baseUrl = DeviceInfo.Platform == DevicePlatform.Android
			? apiSettings["BaseUrlAndroid"]
			: apiSettings["BaseUrlWindows"];
#endif

		if (string.IsNullOrEmpty(baseUrl))
		{
			throw new InvalidOperationException("A URL base da API não foi configurada nas configurações do aplicativo (appsettings.json).");
		}

		builder.Services.AddSingleton(new ApiService(new HttpClient { BaseAddress = new Uri(baseUrl) }));
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ReservoirDetailViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReservoirDetailPage>();

		return builder.Build();
	}
}
