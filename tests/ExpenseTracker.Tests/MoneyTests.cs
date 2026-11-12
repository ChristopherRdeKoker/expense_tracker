using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class MoneyTests
{
    private static string Plain(string formatted) => formatted.Replace(Money.GroupSeparator, ' ');

    [Theory]
    [InlineData("1234.56", "R 1 234.56")]
    [InlineData("0", "R 0.00")]
    [InlineData("5", "R 5.00")]
    [InlineData("999.999", "R 1 000.00")]
    [InlineData("1234567.5", "R 1 234 567.50")]
    [InlineData("100000", "R 100 000.00")]
    [InlineData("-1234.5", "-R 1 234.50")]
    [InlineData("0.005", "R 0.01")]
    public void Format_uses_rand_with_space_groups(string input, string expected)
    {
        Assert.Equal(expected, Plain(Money.Format(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture))));
    }

    [Fact]
    public void Format_uses_non_breaking_spaces_so_amounts_never_wrap()
    {
        Assert.Equal("R 1 234.56", Money.Format(1234.56m));
    }

    [Theory]
    [InlineData("1234.56", "1234.56")]
    [InlineData("1 234.56", "1234.56")]
    [InlineData("1234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("R 99", "99")]
    [InlineData("r12.5", "12.5")]
    [InlineData(" 0.10 ", "0.10")]
    [InlineData("1,234", "1234")]
    public void TryParse_accepts_common_inputs(string input, string expected)
    {
        Assert.True(Money.TryParse(input, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("12.345")]
    [InlineData("1.2.3")]
    [InlineData("1e5")]
    [InlineData("2000000000")]
    public void TryParse_rejects_bad_input(string? input)
    {
        Assert.False(Money.TryParse(input, out _));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("12.30", true)]
    [InlineData("999999999.99", true)]
    [InlineData("1000000000", true)]
    [InlineData("1000000000.01", false)]
    [InlineData("-0.01", false)]
    [InlineData("12.345", false)]
    [InlineData("0.001", false)]
    public void IsStorable_means_zero_to_one_billion_in_whole_cents(string input, bool expected)
    {
        Assert.Equal(expected, Money.IsStorable(TestData.Dec(input)));
    }

    [Theory]
    [InlineData("2.675", "2.68")]
    [InlineData("1.005", "1.01")]
    [InlineData("0.125", "0.13")]
    [InlineData("-1.005", "-1.01")]
    [InlineData("-0.004", "0")]
    [InlineData("0.004", "0")]
    [InlineData("999999999.995", "1000000000.00")]
    public void Round_goes_to_cents_half_away_from_zero(string input, string expected)
    {
        Assert.Equal(TestData.Dec(expected), Money.Round(TestData.Dec(input)));
    }

    [Theory]
    [InlineData("-0.004", "R 0.00")]
    [InlineData("-0.005", "-R 0.01")]
    [InlineData("-0.01", "-R 0.01")]
    [InlineData("999999999.995", "R 1 000 000 000.00")]
    [InlineData("1000000000", "R 1 000 000 000.00")]
    [InlineData("-1999999999.99", "-R 1 999 999 999.99")]
    [InlineData("100.1", "R 100.10")]
    public void Format_edges_keep_two_decimals_and_drop_the_sign_of_zero(string input, string expected)
    {
        Assert.Equal(expected, Plain(Money.Format(TestData.Dec(input))));
    }

    [Theory]
    [InlineData("-1234.5", "-1234.50")]
    [InlineData("0", "0.00")]
    [InlineData("1000000000", "1000000000.00")]
    [InlineData("0.005", "0.01")]
    public void FormatPlain_has_no_grouping_and_two_decimals(string input, string expected)
    {
        Assert.Equal(expected, Money.FormatPlain(TestData.Dec(input)));
    }

    [Theory]
    [InlineData("1 000 000 000", "1000000000")]
    [InlineData("1000000000.00", "1000000000")]
    [InlineData("0,05", "0.05")]
    [InlineData("0", "0")]
    public void TryParse_accepts_the_edges_of_the_range(string input, string expected)
    {
        Assert.True(Money.TryParse(input, out var value));
        Assert.Equal(TestData.Dec(expected), value);
    }

    [Theory]
    [InlineData("1000000000.01")]
    [InlineData("0.001")]
    [InlineData("-0.01")]
    public void TryParse_rejects_just_past_the_edges(string input)
    {
        Assert.False(Money.TryParse(input, out _));
    }
}
