using System.Net;
using System.Net.Http.Headers;
using AquaMap.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaMap.Api.Tests;

// An administrator could not delete a reservoir that had analyses (always 409), and any
// logged-in technician could delete one without analyses. Deleting is now administrator-only
// and removes the reservoir together with its analyses and neighborhoods.
public class DeleteReservoirTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DeleteReservoirTests(ApiFactory factory) => _factory = factory;

    private Task<int> SeedReservoirAsync(int analyses) => _factory.WithDbAsync(async db =>
    {
        var reservoir = new Reservoir { Name = "Reservatório Teste", Latitude = -20.76, Longitude = -41.53 };
        reservoir.Neighborhoods.Add(new Neighborhood { Name = "Centro" });
        for (var i = 0; i < analyses; i++)
            reservoir.WaterAnalyses.Add(new WaterAnalysis
            {
                AnalysisDate = DateTime.UtcNow.AddDays(-i),
                ResidualChlorine = 1.0, Ph = 7.0, Turbidity = 1.0, Iron = 0.1, EColiAbsent = true
            });
        db.Reservoirs.Add(reservoir);
        await db.SaveChangesAsync();
        return reservoir.Id;
    });

    private async Task<HttpResponseMessage> DeleteAsync(int id, string? token, int? expectedAnalyses = null)
    {
        var client = _factory.CreateClient();
        var query = expectedAnalyses is int n ? $"?expectedAnalyses={n}" : "";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/reservoirs/{id}{query}");
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Administrator_deletes_reservoir_with_analyses_and_its_history()
    {
        var id = await SeedReservoirAsync(analyses: 3);

        var response = await DeleteAsync(id, await _factory.TokenForAsync(UserType.Administrator), expectedAnalyses: 3);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var (reservoirs, analyses, neighborhoods) = await _factory.WithDbAsync(async db => (
            await db.Reservoirs.CountAsync(r => r.Id == id),
            await db.WaterAnalyses.CountAsync(w => w.ReservoirId == id),
            await db.Neighborhoods.CountAsync(n => n.ReservoirId == id)));
        Assert.Equal(0, reservoirs);
        Assert.Equal(0, analyses);
        Assert.Equal(0, neighborhoods);
    }

    [Fact]
    public async Task Administrator_deletes_reservoir_without_analyses()
    {
        var id = await SeedReservoirAsync(analyses: 0);

        var response = await DeleteAsync(id, await _factory.TokenForAsync(UserType.Administrator));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // The installed app (v1.0) asks a single "Excluir?" and sends no count: it must keep getting the 409.
    [Fact]
    public async Task Delete_without_confirmed_analysis_count_keeps_the_history()
    {
        var id = await SeedReservoirAsync(analyses: 2);

        var response = await DeleteAsync(id, await _factory.TokenForAsync(UserType.Administrator));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, await _factory.WithDbAsync(db => db.WaterAnalyses.CountAsync(w => w.ReservoirId == id)));
    }

    // A collection synced after the admin opened the confirmation was not counted in it.
    [Fact]
    public async Task Delete_with_outdated_analysis_count_keeps_the_history()
    {
        var id = await SeedReservoirAsync(analyses: 2);

        var response = await DeleteAsync(id, await _factory.TokenForAsync(UserType.Administrator), expectedAnalyses: 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, await _factory.WithDbAsync(db => db.WaterAnalyses.CountAsync(w => w.ReservoirId == id)));
    }

    // Technicians are stored as UserType.Citizen until the Technician role exists (PLANO_V2, Fase 2).
    [Fact]
    public async Task Technician_cannot_delete_a_reservoir()
    {
        var id = await SeedReservoirAsync(analyses: 0);

        var response = await DeleteAsync(id, await _factory.TokenForAsync(UserType.Citizen));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await _factory.WithDbAsync(db => db.Reservoirs.CountAsync(r => r.Id == id)));
    }

    [Fact]
    public async Task Anonymous_cannot_delete_a_reservoir()
    {
        var id = await SeedReservoirAsync(analyses: 0);

        var response = await DeleteAsync(id, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_missing_reservoir_returns_not_found()
    {
        var response = await DeleteAsync(999_999, await _factory.TokenForAsync(UserType.Administrator));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
