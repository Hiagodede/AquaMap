using AquaMap.Domain.Entities;
using AquaMap.Domain.Interfaces;
using AquaMap.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AquaMap.Infrastructure.Repositories
{
    public class CollectionPointRepository : ICollectionPointRepository
    {
        private readonly AppDbContext _dbContext;

        public CollectionPointRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<CollectionPoint>> GetAllAsync()
        {
            // Busca os pontos e inclui o histórico de análises junto (Include)
            return await _dbContext.CollectionPoints
                .Include(p => p.AnalysisHistory)
                .ToListAsync();
        }
    }
}