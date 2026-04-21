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
        private readonly AppDbContext _context;

        public CollectionPointRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Reservoir>> GetAllAsync()
        {
            return await _context.Reservoirs.ToListAsync();
        }
    }
}