using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class MonthNamesTests
{
    [Fact]
    public void December_rolls_into_next_year()
    {
        Assert.Equal("January 2027", MonthNames.NextAvailable(["December 2026"], 2026, 12));
    }

    [Fact]
    public void Taken_names_are_matched_loosely()
    {
        Assert.Equal("December 2026", MonthNames.NextAvailable(["  november   2026 "], 2026, 11));
    }

    [Fact]
    public void Renamed_months_free_their_slot()
    {
        Assert.Equal("November 2026", MonthNames.NextAvailable(["Holiday budget"], 2026, 11));
    }

    [Fact]
    public void Running_out_of_names_fails_loudly()
    {
        var all = Enumerable.Range(0, MonthNames.MaxLookaheadMonths)
            .Select(i => new DateTime(2026, 1, 1).AddMonths(i))
            .Select(d => MonthNames.Format(d.Year, d.Month));
        var ex = Assert.Throws<InvalidOperationException>(() => MonthNames.NextAvailable(all, 2026, 1));
        Assert.Contains("120", ex.Message);
    }
}
