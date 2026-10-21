using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AquaMap.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaMap.Api.Tests;

// A wrong or test analysis stayed in the reservoir history for good: there was no way to fix or remove it.
// Editing and deleting a single analysis is administrator-only, like deleting a reservoir.
public class EditDeleteWaterAnalysisTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EditDeleteWaterAnalysisTests(ApiFactory factory) => _factory = factory;

    private static readonly object ValidReading = new { ResidualChlorine = 1.2, Ph = 7.4, Turbidity = 0.8, Iron = 0.05, EColiAbsent = true };

    // Returns the ids of the reservoir and of its analyses, newest first.
    private Task<(int ReservoirId, int[] AnalysisIds)> SeedAsync(int analyses) => _factory.WithDbAsync(async db =>
    {
        var reservoir = new Reservoir { Name = "Reservatório Teste", Latitude = -20.76, Longitude = -41.53 };
        for (var i = 0; i < analyses; i++)
            reservoir.WaterAnalyses.Add(new WaterAnalysis
            {
                AnalysisDate = new DateTime(2026, 10, 21 - i, 15, 45, 0, DateTimeKind.Utc),
                ResidualChlorine = 1.5, Ph = 7.0, Turbidity = 2.0, Iron = 0.2, EColiAbsent = true,
                CollectionLatitude = -20.77, CollectionLongitude = -41.54
            });
        db.Reservoirs.Add(reservoir);
        await db.SaveChangesAsync();
        return (reservoir.Id, reservoir.WaterAnalyses.OrderByDescending(w => w.AnalysisDate).Select(w => w.Id).ToArray());
    });

    private Task<WaterAnalysis?> FindAsync(int id) =>
        _factory.WithDbAsync(db => db.WaterAnalyses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id));

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, int id, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"/water-analysis/{id}");
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        return await _factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task Administrator_edits_the_readings_of_an_analysis()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Put, ids[0], await _factory.TokenForAsync(UserType.Administrator), ValidReading);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await FindAsync(ids[0]);
        Assert.NotNull(saved);
        Assert.Equal(1.2, saved.ResidualChlorine);
        Assert.Equal(7.4, saved.Ph);
        Assert.Equal(0.8, saved.Turbidity);
        Assert.Equal(0.05, saved.Iron);
        Assert.True(saved.EColiAbsent);
    }

    // Only the readings change: date, reservoir and collection point are the record of the field visit.
    [Fact]
    public async Task Editing_keeps_date_reservoir_and_collection_point()
    {
        var (reservoirId, ids) = await SeedAsync(analyses: 1);
        var before = await FindAsync(ids[0]);

        await SendAsync(HttpMethod.Put, ids[0], await _factory.TokenForAsync(UserType.Administrator),
            new { ResidualChlorine = 1.2, Ph = 7.4, Turbidity = 0.8, Iron = 0.05, EColiAbsent = true, ReservoirId = 999, AnalysisDate = DateTime.UtcNow, CollectionLatitude = 0.0 });

        var after = await FindAsync(ids[0]);
        Assert.Equal(reservoirId, after!.ReservoirId);
        Assert.Equal(before!.AnalysisDate, after.AnalysisDate);
        Assert.Equal(before.CollectionLatitude, after.CollectionLatitude);
        Assert.Equal(before.CollectionLongitude, after.CollectionLongitude);
    }

    // Rule 4 of CLAUDE.md: an out-of-standard reading is an alert to record, not an error.
    [Fact]
    public async Task Editing_to_an_out_of_standard_reading_is_accepted()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Put, ids[0], await _factory.TokenForAsync(UserType.Administrator),
            new { ResidualChlorine = 0.1, Ph = 10.0, Turbidity = 8.0, Iron = 0.5, EColiAbsent = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await FindAsync(ids[0]))!.IsPotable);
    }

    [Fact]
    public async Task Editing_to_a_physically_impossible_reading_is_rejected()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Put, ids[0], await _factory.TokenForAsync(UserType.Administrator),
            new { ResidualChlorine = 1.2, Ph = 15.0, Turbidity = 0.8, Iron = 0.05, EColiAbsent = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(7.0, (await FindAsync(ids[0]))!.Ph);
    }

    // A missing field used to become 0/false: no E. coli field recorded "present", no iron hid an alert.
    [Fact]
    public async Task Editing_with_a_missing_reading_is_rejected()
    {
        var (_, ids) = await SeedAsync(analyses: 1);
        var token = await _factory.TokenForAsync(UserType.Administrator);

        var withoutEColi = await SendAsync(HttpMethod.Put, ids[0], token, new { ResidualChlorine = 1.2, Ph = 7.4, Turbidity = 0.8, Iron = 0.05 });
        var withoutIron = await SendAsync(HttpMethod.Put, ids[0], token, new { ResidualChlorine = 1.2, Ph = 7.4, Turbidity = 0.8, EColiAbsent = true });

        Assert.Equal(HttpStatusCode.BadRequest, withoutEColi.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutIron.StatusCode);
        var saved = await FindAsync(ids[0]);
        Assert.True(saved!.EColiAbsent);
        Assert.Equal(0.2, saved.Iron);
    }

    [Fact]
    public async Task Editing_a_missing_analysis_returns_not_found()
    {
        var response = await SendAsync(HttpMethod.Put, 999_999, await _factory.TokenForAsync(UserType.Administrator), ValidReading);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Technicians are stored as UserType.Citizen until the Technician role exists.
    [Fact]
    public async Task Technician_cannot_edit_an_analysis()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Put, ids[0], await _factory.TokenForAsync(UserType.Citizen), ValidReading);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1.5, (await FindAsync(ids[0]))!.ResidualChlorine);
    }

    [Fact]
    public async Task Anonymous_cannot_edit_an_analysis()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Put, ids[0], token: null, ValidReading);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1.5, (await FindAsync(ids[0]))!.ResidualChlorine);
    }

    [Fact]
    public async Task Administrator_deletes_only_the_chosen_analysis()
    {
        var (reservoirId, ids) = await SeedAsync(analyses: 2);

        var response = await SendAsync(HttpMethod.Delete, ids[0], await _factory.TokenForAsync(UserType.Administrator));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await FindAsync(ids[0]));
        Assert.NotNull(await FindAsync(ids[1]));
        Assert.Equal(1, await _factory.WithDbAsync(db => db.Reservoirs.CountAsync(r => r.Id == reservoirId)));
    }

    [Fact]
    public async Task Deleting_a_missing_analysis_returns_not_found()
    {
        var response = await SendAsync(HttpMethod.Delete, 999_999, await _factory.TokenForAsync(UserType.Administrator));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Technician_cannot_delete_an_analysis()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Delete, ids[0], await _factory.TokenForAsync(UserType.Citizen));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await FindAsync(ids[0]));
    }

    [Fact]
    public async Task Anonymous_cannot_delete_an_analysis()
    {
        var (_, ids) = await SeedAsync(analyses: 1);

        var response = await SendAsync(HttpMethod.Delete, ids[0], token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(await FindAsync(ids[0]));
    }
}
