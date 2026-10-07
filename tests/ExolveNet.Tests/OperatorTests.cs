using Xunit;

namespace ExolveNet.Tests;

public sealed class OperatorTests
{
    [Theory]
    [InlineData("01", MobileOperator.Mts)]
    [InlineData("02", MobileOperator.Megafon)]
    [InlineData("99", MobileOperator.Beeline)]
    [InlineData("20", MobileOperator.Tele2)]
    public void Resolves_the_four_federal_operators_from_their_network_code(string mnc, MobileOperator expected) =>
        Assert.Equal(expected, ExolveOperators.Resolve(mnc));

    [Theory]
    [InlineData("11", MobileOperator.Yota)]
    [InlineData("62", MobileOperator.TinkoffMobile)]
    public void Resolves_the_two_mvnos_confirmed_against_the_live_api(string mnc, MobileOperator expected) =>
        // Verified against the live API.
        Assert.Equal(expected, ExolveOperators.Resolve(mnc));

    [Fact]
    public void A_valid_but_unmapped_code_is_Other_not_Unknown()
    {
        // We know who it isn't; claiming a name we are not sure of would be worse.
        Assert.Equal(MobileOperator.Other, ExolveOperators.Resolve("35"));
    }

    [Fact]
    public void Nothing_to_go_on_is_Unknown()
    {
        Assert.Equal(MobileOperator.Unknown, ExolveOperators.Resolve(mnc: null));
        Assert.Equal(MobileOperator.Unknown, ExolveOperators.Resolve(mnc: null, ownerId: "  "));
    }

    [Theory]
    [InlineData("MTS", MobileOperator.Mts)]
    [InlineData("мтс", MobileOperator.Mts)]
    [InlineData("MegaFon", MobileOperator.Megafon)]
    [InlineData("Билайн", MobileOperator.Beeline)]
    [InlineData("Tele2", MobileOperator.Tele2)]
    [InlineData("Мотив", MobileOperator.Other)]
    public void Falls_back_to_the_name_when_there_is_no_network_code(string ownerId, MobileOperator expected) =>
        Assert.Equal(expected, ExolveOperators.Resolve(mnc: null, ownerId));

    [Fact]
    public void The_network_code_wins_over_a_contradictory_name()
    {
        // owner_id is free text the provider can write any way; the MNC is standardised.
        Assert.Equal(MobileOperator.Tele2, ExolveOperators.Resolve("20", ownerId: "MTS"));
    }
}
