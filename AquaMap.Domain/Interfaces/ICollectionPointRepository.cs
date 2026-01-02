using AquaMap.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AquaMap.Domain.Interfaces
{
    public interface ICollectionPointRepository
    {
        // Contrato: Quero um método que me devolva todos os pontos
        Task<List<CollectionPoint>> GetAllAsync();
    }
}