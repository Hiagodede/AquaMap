using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Devices;

namespace AquaMap.Client.Shared;

/// <summary>
/// Builds the HttpClient used to talk to the AquaMap API, reading ApiSettings from the
/// calling app's own embedded appsettings.json / appsettings.Development.json.
/// </summary>
public static class ApiClientFactory
{
    public static HttpClient Create(Assembly callingAssembly)
    {
        var prefix = callingAssembly.GetName().Name;

        var configBuilder = new ConfigurationBuilder();

        using var stream = callingAssembly.GetManifestResourceStream($"{prefix}.appsettings.json");
        if (stream != null)
        {
            configBuilder.AddJsonStream(stream);
        }

#if DEBUG
        using var devStream = callingAssembly.GetManifestResourceStream($"{prefix}.appsettings.Development.json");
        if (devStream != null)
        {
            configBuilder.AddJsonStream(devStream);
        }
#endif

        var configuration = configBuilder.Build();
        var apiSettings = configuration.GetSection("ApiSettings");
        string? baseUrl = apiSettings["BaseUrl"];

#if DEBUG
        // Em desenvolvimento local, puxamos os IPs corretos baseados no emulador/sistema
        baseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? apiSettings["BaseUrlAndroid"]
            : apiSettings["BaseUrlWindows"];
#endif

        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                $"A URL base da API não foi configurada em {prefix}.appsettings.json (seção ApiSettings).");
        }

        return new HttpClient { BaseAddress = new Uri(baseUrl) };
    }
}
