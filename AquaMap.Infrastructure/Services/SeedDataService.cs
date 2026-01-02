using AquaMap.Domain.Entities;
using AquaMap.Domain.Interfaces;
using AquaMap.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AquaMap.Infrastructure.Services
{
    public class SeedDataService : ISeedDataService
    {
        private readonly AppDbContext _dbContext;

        public SeedDataService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task InitializeAsync()
        {
            // Garante que o banco foi criado
            await _dbContext.Database.EnsureCreatedAsync();

            // Se já tiver pontos de coleta, não faz nada (já foi populado)
            if (await _dbContext.CollectionPoints.AnyAsync())
                return;

            // 1. Criar Pontos de Coleta em Alegre-ES (Dados fictícios próximos ao real)
            var pontoPraca = new CollectionPoint(
                "Praça Central",
                -20.7619, // Latitude aprox de Alegre
                -41.5344, // Longitude aprox de Alegre
                "Chafariz principal da praça"
            );

            var pontoRio = new CollectionPoint(
                "Rio Itapemirim - Ponte",
                -20.7630,
                -41.5320,
                "Coleta feita na margem direita"
            );

            // 2. Adicionar Análises (Uma boa e uma ruim)
            // Ponto da Praça: Água Potável
            pontoPraca.AnalysisHistory.Add(new LabAnalysis(
                pontoPraca.Id,
                ph: 7.2,
                turbidity: 2.5,
                totalColiforms: 0,
                hasMetals: false
            ));

            // Ponto do Rio: Água Imprópria (Exemplo)
            pontoRio.AnalysisHistory.Add(new LabAnalysis(
                pontoRio.Id,
                ph: 5.5, // pH baixo
                turbidity: 10.0, // Turbidez alta
                totalColiforms: 50, // Tem coliformes
                hasMetals: false
            ));

            // 3. Criar Usuário Admin
            var admin = new User(
                "Administrador",
                "000.000.000-00",
                DateTime.Now.AddYears(-30),
                "UFES",
                "2899999999",
                "admin@aquamap.com",
                "admin123", // Num app real, isso seria criptografado!
                UserType.Administrator
            );

            // Adiciona tudo ao banco
            await _dbContext.CollectionPoints.AddRangeAsync(pontoPraca, pontoRio);
            await _dbContext.Users.AddAsync(admin);

            await _dbContext.SaveChangesAsync();
        }
    }
}