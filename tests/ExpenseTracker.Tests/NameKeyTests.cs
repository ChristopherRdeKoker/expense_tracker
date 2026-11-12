using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class NameKeyTests
{
    [Theory]
    [InlineData("  Eating   out ", "Eating out")]
    [InlineData("Eating\tout\n", "Eating out")]
    [InlineData("Eating out", "Eating out")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Collapse_spaces_keeps_case_and_single_spaces(string? input, string expected)
    {
        Assert.Equal(expected, NameKey.CollapseSpaces(input));
    }

    [Theory]
    [InlineData("  NOVEMBER   2026 ", "november 2026")]
    [InlineData("Eating Out", "eating out")]
    [InlineData("   ", "")]
    public void Normalise_lowercases_and_collapses_spaces(string input, string expected)
    {
        Assert.Equal(expected, NameKey.Normalise(input));
    }

    [Fact]
    public void Clean_name_collapses_spaces_and_cuts_at_the_max_length()
    {
        var longName = new string('x', LedgerOps.MaxNameLength) + "yz";

        Assert.Equal("Bonus month", LedgerOps.CleanName("  Bonus \t month "));
        Assert.Equal(new string('x', LedgerOps.MaxNameLength), LedgerOps.CleanName(longName));
    }
}
