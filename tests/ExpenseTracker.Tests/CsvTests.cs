using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class CsvTests
{
    [Fact]
    public void Export_has_header_and_plain_amounts()
    {
        var ledger = TestData.Ledger(out _);
        var lines = LedgerCsv.Export(ledger).Split("\r\n");
        Assert.Equal(LedgerCsv.Header, lines[0]);
        Assert.Equal("March 2026,Income,Salary,,20000.00", lines[1]);
        Assert.Equal("March 2026,Expense,Shop,Food,600.25", lines[3]);
    }

    [Fact]
    public void Round_trip_keeps_rows_and_totals()
    {
        var ledger = Seed.Sample(2026, 11);
        LedgerOps.AddMonth(ledger, 2026, 11);
        var tricky = ledger.Months[0];
        LedgerOps.AddEntry(tricky, EntryKind.Expense, "Dinner, \"the good one\"", 1234.56m, ledger.Categories[0].Id);
        LedgerOps.AddEntry(tricky, EntryKind.Expense, "=HYPERLINK(\"x\")", 1m, null);
        LedgerOps.AddEntry(tricky, EntryKind.Expense, "-minus first", 2m, null);

        var csv = LedgerCsv.Export(ledger);
        var back = LedgerCsv.ToLedger(LedgerCsv.Parse(csv), ledger.Categories);

        Assert.Equal(csv, LedgerCsv.Export(back));
        Assert.Equal(ledger.Months.Select(m => m.Name), back.Months.Select(m => m.Name));
        for (var i = 0; i < ledger.Months.Count; i++)
        {
            Assert.Equal(LedgerMath.Totals(ledger.Months[i].Entries), LedgerMath.Totals(back.Months[i].Entries));
        }
        Assert.Contains(back.Months[0].Entries, e => e.Name == "=HYPERLINK(\"x\")");
        Assert.Equal(ledger.Categories[0].Budget, back.Categories[0].Budget);
    }

    [Fact]
    public void Formula_like_names_are_guarded_in_the_file()
    {
        var ledger = new Ledger();
        var month = LedgerOps.AddMonth(ledger, 2026, 1);
        LedgerOps.AddEntry(month, EntryKind.Expense, "=1+1", 1m, null);
        Assert.Contains(",'=1+1,", LedgerCsv.Export(ledger));
    }

    [Fact]
    public void Unknown_categories_are_created_without_budget()
    {
        var rows = LedgerCsv.Parse("Month,Kind,Name,Category,Amount\nJan,Expense,Bread,Bakery,12.50\n");
        var ledger = LedgerCsv.ToLedger(rows);
        var bakery = Assert.Single(ledger.Categories);
        Assert.Equal("Bakery", bakery.Name);
        Assert.Equal(0m, bakery.Budget);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("Name,Amount\nx,1", "first line")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,Expense,x,,abc", "row 2")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,Expense,x,,-3", "not a valid amount")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,Gift,x,,3", "unknown kind")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,1,x,,3", "unknown kind")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,Expense,x,3", "4 columns")]
    [InlineData("Month,Kind,Name,Category,Amount\nJan,Expense,\"x,,3", "quoted field")]
    [InlineData("Month,Kind,Name,Category,Amount\n,Expense,x,,3", "no month")]
    public void Bad_csv_is_rejected_with_a_clear_message(string csv, string expected)
    {
        var ex = Assert.Throws<LedgerFormatException>(() => LedgerCsv.Parse(csv));
        Assert.Contains(expected, ex.Message);
    }
}
