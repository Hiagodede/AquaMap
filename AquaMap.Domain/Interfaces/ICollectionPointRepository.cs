using AquaMap.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AquaMap.Domain.Interfaces
{
    public interface ICollectionPointRepository
    {
        Task<IEnumerable<Reservoir>> GetAllAsync();
    }
}