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
}
