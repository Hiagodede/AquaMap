using System.Net;
using System.Text;
using AquaMap.Client.Shared;
using AquaMap.Domain.Entities;

namespace AquaMap.Tecnico.Tests;

// QA EST-02 / INS-06: any API failure returned an empty list, which the app took as "no reservoirs"
// and used to wipe the offline cache (and empty the collection form's reservoir picker).
public class ApiServiceTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private static ApiService Api(HttpStatusCode status, string body = "") =>
        Api(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static ApiService Api(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("https://api.test") });

    [Fact]
    public async Task Reservoirs_server_error_returns_null_so_the_cache_is_kept()
    {
        Assert.Null(await Api(HttpStatusCode.InternalServerError).GetReservoirsAsync());
    }

    [Fact]
    public async Task Reservoirs_network_failure_returns_null_so_the_cache_is_kept()
    {
        var api = Api(_ => throw new HttpRequestException("sem rede"));

        Assert.Null(await api.GetReservoirsAsync());
    }

    [Fact]
    public async Task Reservoirs_empty_answer_is_a_valid_empty_list()
    {
        var result = await Api(HttpStatusCode.OK, "[]").GetReservoirsAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task History_failure_returns_null_so_the_cache_is_kept()
    {
        Assert.Null(await Api(HttpStatusCode.ServiceUnavailable).GetWaterAnalysisHistoryAsync(1));
    }

    // QA INS-03: a collection rejected by the server must be told apart from a network failure,
    // otherwise it stays "pending" forever.
    [Fact]
    public async Task Rejected_collection_reports_status_and_api_message()
    {
        var api = Api(HttpStatusCode.BadRequest, "{\"error\":\"Ferro deve estar entre 0 e 100 mg/L.\"}");

        var result = await api.SubmitWaterAnalysisAsync(new WaterAnalysis { Iron = -5 }, "token");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Ferro deve estar entre 0 e 100 mg/L.", result.ErrorMessage);
    }

    // The offline queue decision: a wrong answer here deletes field measurements or blocks them forever.
    [Theory]
    [InlineData(true, 201, SubmitOutcome.Accepted)]
    [InlineData(false, 401, SubmitOutcome.SessionExpired)]
    [InlineData(false, 403, SubmitOutcome.SessionExpired)]
    [InlineData(false, 400, SubmitOutcome.Rejected)]
    [InlineData(false, 422, SubmitOutcome.Rejected)]
    [InlineData(false, null, SubmitOutcome.RetryLater)]   // no network
    [InlineData(false, 500, SubmitOutcome.RetryLater)]
    [InlineData(false, 502, SubmitOutcome.RetryLater)]    // Render cold start
    [InlineData(false, 503, SubmitOutcome.RetryLater)]
    [InlineData(false, 404, SubmitOutcome.RetryLater)]
    public void Submit_outcome_drives_the_offline_queue(bool success, int? status, SubmitOutcome expected)
    {
        Assert.Equal(expected, new ApiResult(success, status, null).Outcome);
    }

    [Fact]
    public async Task Collection_network_failure_has_no_status_code()
    {
        var api = Api(_ => throw new HttpRequestException("sem rede"));

        var result = await api.SubmitWaterAnalysisAsync(new WaterAnalysis(), "token");

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
    }

    // The API only deletes a reservoir with analyses when the client confirms how many will go.
    [Fact]
    public async Task Delete_reservoir_sends_the_confirmed_analysis_count()
    {
        HttpRequestMessage? sent = null;
        var api = Api(request => { sent = request; return new HttpResponseMessage(HttpStatusCode.NoContent); });

        var result = await api.DeleteReservoirAsync(7, 3, "token");

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Delete, sent!.Method);
        Assert.Equal("/reservoirs/7?expectedAnalyses=3", sent.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task Delete_reservoir_conflict_is_reported_with_its_status()
    {
        var result = await Api(HttpStatusCode.Conflict, "{\"error\":\"O reservatório possui análises registradas que não foram confirmadas para exclusão.\"}")
            .DeleteReservoirAsync(7, 1, "token");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Update_analysis_sends_only_the_readings()
    {
        HttpRequestMessage? sent = null;
        var api = Api(request => { sent = request; return new HttpResponseMessage(HttpStatusCode.NoContent); });

        var result = await api.UpdateWaterAnalysisAsync(42,
            new WaterAnalysis { ResidualChlorine = 1.2, Ph = 7.4, Turbidity = 0.8, Iron = 0.05, EColiAbsent = false, ReservoirId = 9 }, "token");
        var body = await sent!.Content!.ReadAsStringAsync();

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Put, sent!.Method);
        Assert.Equal("/water-analysis/42", sent.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
        using var json = System.Text.Json.JsonDocument.Parse(body!);
        var fields = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(5, fields.Count); // no reservoir, date or collection point
        Assert.Equal("1.2", fields["ResidualChlorine"]);
        Assert.Equal("7.4", fields["Ph"]);
        Assert.Equal("0.8", fields["Turbidity"]);
        Assert.Equal("0.05", fields["Iron"]);
        Assert.Equal("False", fields["EColiAbsent"]);
    }

    [Fact]
    public async Task Update_analysis_rejection_carries_the_api_message()
    {
        var result = await Api(HttpStatusCode.BadRequest, "{\"error\":\"pH deve estar entre 0 e 14.\"}")
            .UpdateWaterAnalysisAsync(42, new WaterAnalysis(), "token");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("pH deve estar entre 0 e 14.", result.ErrorMessage);
    }

    [Fact]
    public async Task Delete_analysis_targets_the_single_analysis()
    {
        HttpRequestMessage? sent = null;
        var api = Api(request => { sent = request; return new HttpResponseMessage(HttpStatusCode.NoContent); });

        var result = await api.DeleteWaterAnalysisAsync(42, "token");

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Delete, sent!.Method);
        Assert.Equal("/water-analysis/42", sent.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task Delete_analysis_network_failure_has_no_status_code()
    {
        var result = await Api(_ => throw new HttpRequestException("sem rede")).DeleteWaterAnalysisAsync(42, "token");

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
    }
}
