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
}
