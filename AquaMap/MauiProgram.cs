using AquaMap.Infrastructure.Data; // NOVO: Para achar o AppDbContext
using Microsoft.EntityFrameworkCore; // NOVO: Para o EF Core
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

            // --- NOVO: CONFIGURAÇÃO DO BANCO DE DADOS ---
            // Aqui dizemos: "Use o SQLite e salve o arquivo neste caminho"
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Filename={Constants.DatabasePath}"));
            // --------------------------------------------

            return builder.Build();
        }
    }
}