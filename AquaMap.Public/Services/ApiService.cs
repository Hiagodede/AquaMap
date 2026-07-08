using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AquaMap.Domain.Entities;

namespace AquaMap.Public.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        public async Task<List<Reservoir>> GetReservoirsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/reservoirs");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Reservoir>>(_jsonOptions) ?? new List<Reservoir>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro na API: {ex.Message}");
            }
            return new List<Reservoir>();
        }

        public async Task<Reservoir?> GetReservoirByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/reservoirs/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Reservoir>(_jsonOptions);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro na API: {ex.Message}");
            }
            return null;
        }
    }
}
