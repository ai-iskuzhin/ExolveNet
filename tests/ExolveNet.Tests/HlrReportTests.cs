using System.Net;
using System.Text;
using ExolveNet.Hlr;
using Xunit;

namespace ExolveNet.Tests;

public sealed class HlrReportTests
{
    private static ExolveHlrClient Client(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), new ExolveClientOptions { ApiKey = "k" });

    private static StubHttpMessageHandler Ok(string body) => new(HttpStatusCode.OK, body);

    private static string Decode(string base64) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(base64));

    // ── submitting ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Sends_the_number_list_as_base64_not_as_a_json_array()
    {
        var handler = Ok("""{"file_uuid":"f-1"}""");

        var handle = await Client(handler).GenerateActivityScoreReportAsync(
            ["+79139999999", "89139999998"]);

        Assert.Equal("f-1", handle.FileUuid);
        Assert.Equal("https://api.exolve.ru/hlr/v1/GenerateActivityScoreReport",
            handler.LastRequest!.RequestUri!.AbsoluteUri);

        // The wire field is a base64 *file*, and each number is normalized on the way in.
        var sent = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!)
            .RootElement.GetProperty("numbers").GetString()!;
        Assert.Equal("79139999999\n79139999998", Decode(sent));
    }

    [Fact]
    public async Task Best_call_time_reports_go_to_their_own_endpoint()
    {
        var handler = Ok("""{"file_uuid":"f-2"}""");

        await Client(handler).GenerateBestCallTimeReportAsync(["79139999999"]);

        Assert.Equal("https://api.exolve.ru/hlr/v1/GenerateBestCallTimeReport",
            handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task An_empty_list_is_refused_locally()
    {
        var handler = Ok("{}");

        // Exolve answers "at least one number is required" — and bills for it.
        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).GenerateActivityScoreReportAsync([]));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task More_than_fifty_thousand_numbers_is_refused_locally()
    {
        var handler = Ok("{}");
        var tooMany = Enumerable.Range(0, ExolveNumbers.MaxBatchSize + 1).Select(_ => "79139999999");

        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).GenerateActivityScoreReportAsync(tooMany));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task One_bad_number_stops_the_whole_batch_before_it_is_billed()
    {
        var handler = Ok("{}");

        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).GenerateActivityScoreReportAsync(["79139999999", "nonsense"]));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_prepared_base64_payload_can_be_sent_as_is()
    {
        var handler = Ok("""{"file_uuid":"f-3"}""");
        var payload = Convert.ToBase64String("79139999999"u8.ToArray());

        await Client(handler).GenerateReportAsync(HlrReportType.BestCallTime, payload);

        var sent = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!)
            .RootElement.GetProperty("numbers").GetString();
        Assert.Equal(payload, sent);
    }

    [Fact]
    public async Task There_is_no_generator_endpoint_for_an_unknown_report_type()
    {
        var handler = Ok("{}");

        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).GenerateReportAsync(HlrReportType.Unknown, "eA=="));
        Assert.Equal(0, handler.Calls);
    }

    // ── retrieving ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Reads_a_finished_report()
    {
        var results = Convert.ToBase64String("79139999999;0.9"u8.ToArray());
        var handler = Ok($$"""
        {"file_uuid":"f-1","created_at":"2026-11-05T10:00:00Z","type":1,"status":3,
         "number_total":9500,"base64":"{{results}}"}
        """);

        var report = await Client(handler).GetReportAsync("f-1");

        Assert.Equal(HlrReportType.ActivityScore, report.Type);
        Assert.Equal(HlrReportStatus.Ready, report.Status);
        Assert.Equal(9500, report.NumberTotal);
        Assert.True(report.IsComplete);
        Assert.Equal("79139999999;0.9", report.DecodeResults());
        // The wire calls it created_at, but it is the retention deadline.
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero), report.RetainedUntil);
    }

    [Theory]
    [InlineData(1, false)]   // pending
    [InlineData(2, false)]   // processing
    [InlineData(3, true)]    // ready
    [InlineData(4, true)]    // ready, with errors — still has data
    [InlineData(5, false)]   // retention expired
    public async Task Completeness_follows_the_status(int status, bool complete)
    {
        var report = await Client(Ok($$"""{"file_uuid":"f","status":{{status}}}""")).GetReportAsync("f");

        Assert.Equal(complete, report.IsComplete);
    }

    [Fact]
    public async Task A_status_sent_as_a_string_parses_the_same_as_a_number()
    {
        // GetHLRReport documents status as a number, GetHLRListReport as a string — same values.
        var report = await Client(Ok("""{"file_uuid":"f","status":"3","type":"2"}""")).GetReportAsync("f");

        Assert.Equal(HlrReportStatus.Ready, report.Status);
        Assert.Equal(HlrReportType.BestCallTime, report.Type);
    }

    [Fact]
    public async Task An_unrecognised_status_is_Unknown_rather_than_a_parse_failure()
    {
        // A new status at the provider must not break reading the whole report.
        var report = await Client(Ok("""{"file_uuid":"f","status":42}""")).GetReportAsync("f");

        Assert.Equal(HlrReportStatus.Unknown, report.Status);
        Assert.False(report.IsComplete);
    }

    [Fact]
    public async Task Undecodable_results_yield_null_rather_than_throwing()
    {
        var report = await Client(Ok("""{"file_uuid":"f","status":3,"base64":"not base64!!"}""")).GetReportAsync("f");

        Assert.Null(report.DecodeResults());
    }

    [Fact]
    public async Task An_empty_uuid_is_refused_locally()
    {
        var handler = Ok("{}");

        await Assert.ThrowsAsync<ExolveValidationException>(() => Client(handler).GetReportAsync("  "));
        Assert.Equal(0, handler.Calls);
    }

    // ── listing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lists_reports_for_a_period()
    {
        var handler = Ok("""{"reports":[{"file_uuid":"a","status":3},{"file_uuid":"b","status":2}]}""");
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);

        var list = await Client(handler).ListReportsAsync(from, to, limit: 10, offset: 0);

        Assert.Equal(2, list.Reports.Count);
        Assert.Equal("a", list.Reports[0].FileUuid);
        Assert.Equal(HlrReportStatus.Processing, list.Reports[1].Status);
        Assert.Contains("\"date_from\"", handler.LastRequestBody);
        Assert.Contains("\"limit\":10", handler.LastRequestBody);
    }

    [Fact]
    public async Task Optional_paging_is_omitted_rather_than_sent_as_null()
    {
        var handler = Ok("""{"reports":[]}""");

        await Client(handler).ListReportsAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        Assert.DoesNotContain("limit", handler.LastRequestBody);
        Assert.DoesNotContain("offset", handler.LastRequestBody);
    }

    [Fact]
    public async Task A_reversed_period_is_refused_locally()
    {
        var handler = Ok("{}");
        var now = DateTimeOffset.UtcNow;

        // Exolve answers "date_from later than date_to".
        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).ListReportsAsync(now, now.AddDays(-1)));
        Assert.Equal(0, handler.Calls);
    }
}
