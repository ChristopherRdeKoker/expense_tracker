using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class LedgerJsonTests
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
    public void Broken_json_at_the_root_does_not_show_a_bare_path()
    {
        var ex = Assert.Throws<LedgerFormatException>(() => LedgerJson.Deserialize("garbage{"));
        Assert.DoesNotContain("at $", ex.Message);
        Assert.Contains("line 1", ex.Message);
    }

    [Fact]
    public void Two_categories_with_the_same_name_name_the_second_spelling()
    {
        var ex = Assert.Throws<LedgerFormatException>(() => LedgerJson.Deserialize(
            "{\"version\":1,\"categories\":[{\"id\":\"a\",\"name\":\"Food\"},{\"id\":\"b\",\"name\":\" FOOD \"}],\"months\":[]}"));
        Assert.Equal("This is not valid expense data: Two categories are called \" FOOD \".", ex.Message);
    }

    [Fact]
    public void A_valid_empty_ledger_is_accepted()
    {
        var ledger = LedgerJson.Deserialize("{\"version\":1,\"categories\":[],\"months\":[]}");
        Assert.Empty(ledger.Months);
    }

    [Fact]
    public void Round_trip_keeps_edge_amounts_and_awkward_text()
    {
        var ledger = new Ledger();
        var category = LedgerOps.AddCategory(ledger, "Ünïcödé “quotes” \\ back", Money.MaxAmount);
        var month = LedgerOps.AddMonth(ledger, 2026, 12);
        LedgerOps.AddEntry(month, EntryKind.Expense, "</script><b>", 0.01m, category.Id);
        LedgerOps.AddEntry(month, EntryKind.Recurring, "Zero", 0m, null);
        LedgerOps.AddEntry(month, EntryKind.Income, "Twelve fifty", 12.50m, null);

        var json = LedgerJson.Serialize(ledger);
        var back = LedgerJson.Deserialize(json);

        Assert.Equal(json, LedgerJson.Serialize(back));
        Assert.Equal([0.01m, 0m, 12.50m], back.Months[0].Entries.Select(e => e.Amount));
        Assert.Equal("</script><b>", back.Months[0].Entries[0].Name);
        Assert.Equal(Money.MaxAmount, back.Categories[0].Budget);
        Assert.Equal("Ünïcödé “quotes” \\ back", back.Categories[0].Name);
        Assert.Equal(12.50m - 0.01m - 0m, LedgerMath.Totals(back.Months[0].Entries).LeftOver);
    }
}
