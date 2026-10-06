using System.Net;
using Xunit;

namespace ExolveNet.Tests;

public sealed class HlrClientTests
{
    private static ExolveHlrClient Client(StubHttpMessageHandler handler, string version = "v1") =>
        new(new HttpClient(handler), new ExolveClientOptions { ApiKey = "secret-key", HlrApiVersion = version });

    private static StubHttpMessageHandler Ok(string body) => new(HttpStatusCode.OK, body);

    // ── request shape ────────────────────────────────────────────────────────

    [Fact]
    public async Task Posts_the_normalized_number_with_a_bearer_key()
    {
        var handler = Ok("""{"number":79139999999,"result":"0.9"}""");

        await Client(handler).GetActivityScoreAsync("+7 (913) 999-99-99");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.exolve.ru/hlr/v1/GetActivityScore", handler.LastRequest.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("secret-key", handler.LastRequest.Headers.Authorization.Parameter);
        // Exolve wants 7XXXXXXXXXX with no plus; a plus is rejected *and still billed*.
        Assert.Equal("""{"number":"79139999999"}""", handler.LastRequestBody);
    }

    [Fact]
    public async Task A_bad_number_never_reaches_the_network()
    {
        var handler = Ok("{}");

        await Assert.ThrowsAsync<ExolveValidationException>(
            () => Client(handler).GetActivityScoreAsync("12345"));

        // Every HLR request is billed, so the only free rejection is a local one.
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Honours_the_configured_api_version_in_the_path()
    {
        var handler = Ok("""{"number":79139999999,"result":"0.5"}""");

        await Client(handler, version: "v2").GetActivityScoreAsync("79139999999");

        Assert.Equal("https://api.exolve.ru/hlr/v2/GetActivityScore", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("GetSimStatus")]
    [InlineData("GetBaseNumberInfo")]
    [InlineData("GetLastDateActivity")]
    [InlineData("GetBestCallTime")]
    [InlineData("GetBestSmsTime")]
    public async Task Each_method_targets_its_own_path(string method)
    {
        var handler = Ok("""{"number":79139999999,"is_registered":true,"days_since_last_activity":"2","result":"17:00:00,21:00:00"}""");
        var client = Client(handler);

        _ = method switch
        {
            "GetSimStatus" => (object)await client.GetSimStatusAsync("79139999999"),
            "GetBaseNumberInfo" => await client.GetBaseNumberInfoAsync("79139999999"),
            "GetLastDateActivity" => await client.GetLastDateActivityAsync("79139999999"),
            "GetBestCallTime" => await client.GetBestCallTimeAsync("79139999999"),
            _ => await client.GetBestSmsTimeAsync("79139999999"),
        };

        Assert.Equal($"https://api.exolve.ru/hlr/v1/{method}", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    // ── responses ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reads_an_activity_score()
    {
        var handler = Ok("""{"number":79139999999,"result":"0.85","owner_id":"MTS","region_code":54}""");

        var result = await Client(handler).GetActivityScoreAsync("79139999999");

        Assert.Equal(79139999999UL, result.Number);
        Assert.Equal("0.85", result.Result);
        Assert.Equal(0.85, result.Score);
        Assert.Equal("MTS", result.OwnerId);
        Assert.Equal(54u, result.RegionCode);
        // No network_code on this method, so the operator comes from the name.
        Assert.Equal(MobileOperator.Mts, result.Operator);
    }

    [Fact]
    public async Task An_unparseable_score_stays_raw_rather_than_becoming_zero()
    {
        var handler = Ok("""{"number":79139999999,"result":"n/a"}""");

        var result = await Client(handler).GetActivityScoreAsync("79139999999");

        // Silently reporting 0 would read as "completely inactive" — the opposite of "unknown".
        Assert.Null(result.Score);
        Assert.Equal("n/a", result.Result);
    }

    [Fact]
    public async Task Reads_a_sim_status()
    {
        var handler = Ok("""{"number":79139999999,"is_registered":true}""");

        Assert.True((await Client(handler).GetSimStatusAsync("79139999999")).IsRegistered);
    }

    [Fact]
    public async Task Reads_base_number_info_and_splits_the_network_code()
    {
        var handler = Ok("""{"number":79139999999,"owner_id":"Tele2","region_code":54,"network_code":"25020","mnp":true}""");

        var result = await Client(handler).GetBaseNumberInfoAsync("79139999999");

        Assert.Equal("Tele2", result.OwnerId);
        Assert.Equal("25020", result.NetworkCode);
        Assert.Equal("250", result.Mcc);   // Russia
        Assert.Equal("20", result.Mnc);    // Tele2
        // Ported numbers are exactly why a prefix table cannot answer "which operator".
        Assert.True(result.IsPorted);
        Assert.Equal(MobileOperator.Tele2, result.Operator);
    }

    [Fact]
    public async Task Reads_last_activity_in_days()
    {
        var handler = Ok("""{"number":79139999999,"days_since_last_activity":"2"}""");

        var result = await Client(handler).GetLastDateActivityAsync("79139999999");

        Assert.Equal("2", result.DaysSinceLastActivity);
        Assert.Equal(2, result.Days);
    }

    [Fact]
    public async Task Reads_a_best_time_interval_and_parses_both_bounds()
    {
        var handler = Ok("""{"number":79139999999,"result":"17:00:00,21:00:00"}""");

        var result = await Client(handler).GetBestCallTimeAsync("79139999999");

        Assert.Equal(new TimeSpan(17, 0, 0), result.From);
        Assert.Equal(new TimeSpan(21, 0, 0), result.To);
    }

    [Theory]
    [InlineData("""{"number":79139999999,"result":"garbage"}""")]
    [InlineData("""{"number":79139999999,"result":"17:00:00"}""")]
    [InlineData("""{"number":79139999999}""")]
    public async Task A_malformed_interval_yields_nulls_not_wrong_times(string body)
    {
        var result = await Client(Ok(body)).GetBestSmsTimeAsync("79139999999");

        Assert.Null(result.From);
        Assert.Null(result.To);
    }

    // ── errors ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_non_mts_number_is_flagged_rather_than_left_as_a_string_to_match()
    {
        // GetSimStatus / GetBestCallTime / GetBestSmsTime serve MTS numbers only.
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, """{"error":"enter another number"}""");

        var ex = await Assert.ThrowsAsync<ExolveApiException>(
            () => Client(handler).GetSimStatusAsync("79139999999"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        Assert.True(ex.IsNonMtsNumber);
        Assert.False(ex.IsHlrDisabled);
        Assert.False(ex.IsUnsignedCustomer);
    }

    [Fact]
    public async Task Hlr_disabled_on_the_application_is_its_own_signal()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest, """{"error":"hlr is disabled on the application"}""");

        var ex = await Assert.ThrowsAsync<ExolveApiException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.True(ex.IsHlrDisabled);
        Assert.False(ex.IsNonMtsNumber);
    }

    [Fact]
    public async Task The_trial_period_restriction_is_distinguishable_from_an_operator_limit()
    {
        // Easy to misread as "wrong operator" — it means "not signed yet, own number only".
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"error":"customer has not signed, the number must match your confirmed phone number"}""");

        var ex = await Assert.ThrowsAsync<ExolveApiException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.True(ex.IsUnsignedCustomer);
        Assert.False(ex.IsNonMtsNumber);
    }

    [Fact]
    public async Task A_bad_token_surfaces_as_an_api_exception_with_the_status()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.Unauthorized, """{"error":"incorrect authorization token"}""");

        var ex = await Assert.ThrowsAsync<ExolveApiException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.HttpStatusCode);
        Assert.Equal("incorrect authorization token", ex.Error!.Text);
    }

    [Fact]
    public async Task A_non_json_error_body_still_produces_an_api_exception()
    {
        // 404 from Exolve is plain text, not the error schema.
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound, "404 page not found", "text/plain");

        var ex = await Assert.ThrowsAsync<ExolveApiException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.Equal(HttpStatusCode.NotFound, ex.HttpStatusCode);
    }

    [Fact]
    public async Task An_empty_body_is_a_protocol_error()
    {
        var ex = await Assert.ThrowsAsync<ExolveProtocolException>(
            () => Client(Ok("")).GetActivityScoreAsync("79139999999"));

        Assert.Equal(HttpStatusCode.OK, ex.HttpStatusCode);
    }

    [Fact]
    public async Task A_transport_failure_is_wrapped_not_leaked()
    {
        var handler = StubHttpMessageHandler.Throwing(
            new HttpRequestException("The SSL connection could not be established."));

        var ex = await Assert.ThrowsAsync<ExolveTransportException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task The_body_preview_masks_the_number()
    {
        // Exception text lands in logs; a phone number is personal data.
        var handler = Ok("""{"number":"79139999999","result":[1,2,3]}""");

        var ex = await Assert.ThrowsAsync<ExolveProtocolException>(
            () => Client(handler).GetActivityScoreAsync("79139999999"));

        Assert.DoesNotContain("79139999999", ex.BodyPreview);
        Assert.Contains("***", ex.BodyPreview);
    }

    // ── metadata ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Raw_body_is_withheld_unless_asked_for()
    {
        var handler = Ok("""{"number":79139999999,"result":"0.9"}""");

        var off = await Client(handler).GetActivityScoreAsync("79139999999");
        Assert.Equal(HttpStatusCode.OK, off.Metadata!.HttpStatusCode);
        // It contains a phone number, so it is opt-in.
        Assert.Null(off.Metadata.RawBody);

        var on = new ExolveHlrClient(
            new HttpClient(handler),
            new ExolveClientOptions { ApiKey = "k", CaptureRawResponseBody = true });
        Assert.NotNull((await on.GetActivityScoreAsync("79139999999")).Metadata!.RawBody);
    }

    [Fact]
    public void A_missing_api_key_fails_at_construction()
    {
        Assert.Throws<ArgumentException>(() =>
            new ExolveHlrClient(new HttpClient(), new ExolveClientOptions { ApiKey = "  " }));
    }
}
