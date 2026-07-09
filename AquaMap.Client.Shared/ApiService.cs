using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AquaMap.Domain.Entities;

namespace AquaMap.Client.Shared;

/// <summary>
/// Cliente HTTP para a AquaMap.Api. Compartilhado pelo app técnico (CRUD completo) e pelo
/// app cidadão (apenas os métodos de leitura são usados, mas ambos falam com a mesma API).
/// </summary>
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
            var response = await _httpClient.GetAsync("/reservoirs").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Reservoir>>(_jsonOptions).ConfigureAwait(false) ?? new List<Reservoir>();
            }
            return new List<Reservoir>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar reservatórios: {ex.Message}");
            return new List<Reservoir>();
        }
    }

    public async Task<Reservoir?> GetReservoirByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/reservoirs/{id}").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Reservoir>(_jsonOptions).ConfigureAwait(false);
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar reservatório: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> LoginAsync(string taxId, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/login", new { TaxId = taxId, Password = password }).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions).ConfigureAwait(false);
                return result?.Token;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro no login: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SubmitWaterAnalysisAsync(WaterAnalysis analysis, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/water-analysis");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(analysis);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao submeter análise: {ex.Message}");
            return false;
        }
    }

    public async Task<List<WaterAnalysis>> GetWaterAnalysisHistoryAsync(int reservoirId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/water-analysis/{reservoirId}").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<WaterAnalysis>>(_jsonOptions).ConfigureAwait(false) ?? new List<WaterAnalysis>();
            }
            return new List<WaterAnalysis>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar histórico: {ex.Message}");
            return new List<WaterAnalysis>();
        }
    }

    public async Task<bool> CreateReservoirAsync(string name, double latitude, double longitude, string token, List<string>? neighborhoodNames = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/reservoirs");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                Name = name,
                Latitude = latitude,
                Longitude = longitude,
                NeighborhoodNames = neighborhoodNames ?? new List<string>()
            });

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao criar reservatório: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateReservoirAsync(int id, string name, double latitude, double longitude, string token, List<string>? neighborhoodNames = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/reservoirs/{id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                Name = name,
                Latitude = latitude,
                Longitude = longitude,
                NeighborhoodNames = neighborhoodNames ?? new List<string>()
            });

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao atualizar reservatório: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteReservoirAsync(int id, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/reservoirs/{id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao excluir reservatório: {ex.Message}");
            return false;
        }
    }

    public async Task<List<UserDto>> GetUsersAsync(string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/users");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<UserDto>>(_jsonOptions).ConfigureAwait(false) ?? new List<UserDto>();
            }
            return new List<UserDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar usuários: {ex.Message}");
            return new List<UserDto>();
        }
    }

    public async Task<bool> CreateUserAsync(object userData, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/users");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(userData);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao criar usuário: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid id, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/users/{id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao excluir usuário: {ex.Message}");
            return false;
        }
    }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int Role { get; set; }
    public string RoleLabel => Role == 1 ? "Administrador" : "Técnico";
}
