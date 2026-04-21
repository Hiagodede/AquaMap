using System.Threading.Tasks;

namespace AquaMap.Domain.Interfaces
{
    public interface ISeedDataService
    {
        Task InitializeAsync();
    }
}
