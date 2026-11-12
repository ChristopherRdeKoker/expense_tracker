using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class JsonTests
{
    [Fact]
    public void Round_trip_keeps_everything()
    {
        var ledger = Seed.Sample(2026, 11);
        LedgerOps.AddMonth(ledger, 2026, 11);
        var json = LedgerJson.Serialize(ledger);
        var back = LedgerJson.Deserialize(json);
        Assert.Equal(json, LedgerJson.Serialize(back));
        Assert.Equal(ledger.Months.Count, back.Months.Count);
        Assert.Equal(LedgerMath.Totals(ledger.Months[1].Entries), LedgerMath.Totals(back.Months[1].Entries));
        Assert.Contains("\"kind\": \"Recurring\"", json);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("not json at all", "JSON is broken")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[]", "JSON is broken")]
    [InlineData("[]", "wrong shape")]
    [InlineData("\"hello\"", "wrong shape")]
    [InlineData("null", "no ledger")]
    [InlineData("{}", "wrong shape")]
    [InlineData("{\"version\":1,\"categories\":[]}", "wrong shape")]
    [InlineData("{\"version\":2,\"categories\":[],\"months\":[]}", "newer version")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"M\",\"entries\":[{\"id\":\"e\",\"kind\":\"Expense\",\"name\":\"x\",\"amount\":-5}]}]}", "negative")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"M\",\"entries\":[{\"id\":\"e\",\"kind\":\"Expense\",\"name\":\"x\",\"amount\":\"5\"}]}]}", "wrong shape")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"M\",\"entries\":[{\"id\":\"e\",\"kind\":\"Bribe\",\"name\":\"x\",\"amount\":5}]}]}", "wrong shape")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"M\",\"entries\":[{\"id\":\"e\",\"kind\":\"Expense\",\"name\":\"x\",\"amount\":5,\"categoryId\":\"ghost\"}]}]}", "does not exist")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"M\",\"entries\":[{\"id\":\"e\",\"kind\":\"Expense\",\"name\":\"x\",\"amount\":5.001}]}]}", "two decimals")]
    [InlineData("{\"version\":1,\"categories\":[{\"id\":\"c\",\"name\":\"A\",\"budget\":1},{\"id\":\"c\",\"name\":\"B\",\"budget\":1}],\"months\":[]}", "reuses the id")]
    [InlineData("{\"version\":1,\"categories\":[{\"id\":\"c\",\"name\":\"A\",\"budget\":1,\"color\":42}],\"months\":[]}", "colour")]
    [InlineData("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"name\":\"  \",\"entries\":[]}]}", "no name")]
    public void Corrupt_data_is_rejected_with_a_clear_message(string json, string expected)
    {
        var ex = Assert.Throws<LedgerFormatException>(() => LedgerJson.Deserialize(json));
        Assert.Contains(expected, ex.Message);
        Assert.DoesNotContain("System.", ex.Message);
    }

    [Fact]
    public void Missing_month_name_points_at_the_problem()
    {
        var ex = Assert.Throws<LedgerFormatException>(() =>
            LedgerJson.Deserialize("{\"version\":1,\"categories\":[],\"months\":[{\"id\":\"m\",\"entries\":[]}]}"));
        Assert.Contains("$.months[0]", ex.Message);
    }

    [Fact]
    public void A_valid_empty_ledger_is_accepted()
    {
        var ledger = LedgerJson.Deserialize("{\"version\":1,\"categories\":[],\"months\":[]}");
        Assert.Empty(ledger.Months);
    }
}
