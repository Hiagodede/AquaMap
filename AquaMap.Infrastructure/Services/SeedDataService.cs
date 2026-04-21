using AquaMap.Domain.Interfaces;
using System.Threading.Tasks;

namespace AquaMap.Infrastructure.Services
{
    public class SeedDataService : ISeedDataService
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}