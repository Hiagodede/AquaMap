using AquaMap.Domain.Interfaces;         
using AquaMap.Infrastructure.Data; 
using AquaMap.Infrastructure.Repositories;
using AquaMap.Infrastructure.Services;   
using AquaMap.ViewModels;  
using Microsoft.EntityFrameworkCore; 
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


            // Configuração do Banco (Você já fez)
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Filename={Constants.DatabasePath}"));

            // --- NOVO: REGISTRAR O SEEDER ---
            builder.Services.AddTransient<ISeedDataService, SeedDataService>();

            // --- NOVO: REGISTRAR O REPOSITÓRIO ---
            builder.Services.AddScoped<ICollectionPointRepository, CollectionPointRepository>();
            // -------------------------------------

            // --- NOVO: REGISTRAR TELA E VIEWMODEL ---
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();

            var app = builder.Build();

            // --- NOVO: EXECUTAR O SEEDER ---
            // Isso cria um escopo temporário para rodar o serviço de banco
            SeedData(app);

            return app;
        }

        // Método auxiliar para rodar o Seed
        private static void SeedData(MauiApp app)
        {
            var scopedFactory = app.Services.GetService<IServiceScopeFactory>();
            using (var scope = scopedFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<ISeedDataService>();
                // O .Wait() é usado aqui porque estamos num contexto síncrono de inicialização
                service.InitializeAsync().Wait();
            }
        }
    }
}