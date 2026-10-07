using Xunit;

namespace ExolveNet.Tests;

public sealed class NumberTests
{
    [Theory]
    [InlineData("79139999999", "79139999999")]
    [InlineData("+79139999999", "79139999999")]
    [InlineData("89139999999", "79139999999")]
    [InlineData("+7 (913) 999-99-99", "79139999999")]
    [InlineData("8 913 999 99 99", "79139999999")]
    public void Normalizes_the_shapes_callers_actually_store(string input, string expected) =>
        Assert.Equal(expected, ExolveNumbers.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7913999999")]        // 10 digits
    [InlineData("791399999999")]      // 12 digits
    [InlineData("49139999999")]       // not +7
    [InlineData("not a number")]
    public void Rejects_anything_that_is_not_a_russian_number(string? input) =>
        Assert.Throws<ExolveValidationException>(() => ExolveNumbers.Normalize(input));

    [Fact]
    public void Renders_a_response_number_back_as_e164() =>
        Assert.Equal("+79139999999", ExolveNumbers.ToE164(79139999999));

    // ── пакетный файл ────────────────────────────────────────────────────────

    private static string Decode(string base64) =>
        System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(base64));

    /// <summary>
    /// Exolve разбирает файл как CSV и первую строку считает заголовком. Без него теряется
    /// первый номер списка — молча: отчёт приходит со статусом «готов», просто на один номер
    /// короче. Проверяется именно это, а не форма base64.
    /// </summary>
    [Fact]
    public void The_batch_file_starts_with_a_header_line_that_exolve_will_skip()
    {
        var text = Decode(ExolveNumbers.EncodeNumberList(["+79139999901", "89139999902"]));
        var lines = text.Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.Equal("Number", lines[0]);
        Assert.Equal("79139999901", lines[1]);
        Assert.Equal("79139999902", lines[2]);
    }

    /// <summary>
    /// Один номер — тоже валидный список. Без заголовка Exolve отвечал на такой файл
    /// «at least one number is required»: единственная строка уходила в заголовок.
    /// </summary>
    [Fact]
    public void A_single_number_still_leaves_one_number_after_the_header()
    {
        var lines = Decode(ExolveNumbers.EncodeNumberList(["+79139999901"])).Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal("Number", lines[0]);
        Assert.Equal("79139999901", lines[1]);
    }

    [Fact]
    public void An_empty_list_is_refused_before_the_request_is_billed()
    {
        Assert.Throws<ExolveValidationException>(() => ExolveNumbers.EncodeNumberList([]));
        Assert.Throws<ExolveValidationException>(() => ExolveNumbers.EncodeNumberList(null!));
    }

    [Fact]
    public void Every_number_in_the_list_is_normalized_and_a_bad_one_stops_the_batch()
    {
        Assert.Throws<ExolveValidationException>(
            () => ExolveNumbers.EncodeNumberList(["+79139999901", "not a number"]));
    }

    [Fact]
    public void A_list_above_the_provider_limit_is_refused()
    {
        var tooMany = Enumerable.Range(0, ExolveNumbers.MaxBatchSize + 1)
            .Select(i => $"791{i:D8}");

        Assert.Throws<ExolveValidationException>(() => ExolveNumbers.EncodeNumberList(tooMany));
    }
}
