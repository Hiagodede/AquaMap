using AquaMap.Domain.Entities;
using System.Net.Http.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Maui.Devices;

namespace AquaMap.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Determina a URL base dependendo de onde o app está rodando (Windows vs Emulador Android)
            var baseUrl = DeviceInfo.Platform == DevicePlatform.Android 
                ? "http://10.0.2.2:5059" 
                : "http://localhost:5059";

            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<List<Reservoir>> GetReservoirsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/reservoirs");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<Reservoir>>() ?? new List<Reservoir>();
                }
                return new List<Reservoir>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao buscar reservatórios: {ex.Message}");
                return new List<Reservoir>();
            }
        }
    }
}
