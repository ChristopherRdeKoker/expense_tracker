using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class RolloverTests
{
    [Fact]
    public void First_month_is_the_current_month_and_empty()
    {
        var ledger = new Ledger();
        var month = LedgerOps.AddMonth(ledger, 2026, 11);
        Assert.Equal("November 2026", month.Name);
        Assert.Empty(month.Entries);
    }

    [Fact]
    public void New_month_carries_recurring_rows_only_with_fresh_ids()
    {
        var ledger = TestData.Ledger(out var march);
        var next = LedgerOps.AddMonth(ledger, 2026, 3);

        Assert.Same(next, ledger.Months[0]);
        Assert.Equal("April 2026", next.Name);
        var carried = Assert.Single(next.Entries);
        Assert.Equal(EntryKind.Recurring, carried.Kind);
        Assert.Equal("Rent", carried.Name);
        Assert.Equal(5000m, carried.Amount);
        Assert.Equal(march.Entries.Single(e => e.Kind == EntryKind.Recurring).CategoryId, carried.CategoryId);
        Assert.DoesNotContain(march.Entries, e => e.Id == carried.Id);
    }

    [Fact]
    public void Editing_a_carried_row_does_not_touch_last_month()
    {
        var ledger = TestData.Ledger(out var march);
        var next = LedgerOps.AddMonth(ledger, 2026, 3);
        var edited = next.Entries[0].Clone();
        edited.Amount = 5500m;
        LedgerOps.UpdateEntry(next, edited);
        Assert.Equal(5000m, march.Entries.Single(e => e.Kind == EntryKind.Recurring).Amount);
    }

    [Fact]
    public void Rollover_copies_from_the_newest_month()
    {
        var ledger = new Ledger();
        var jan = LedgerOps.AddMonth(ledger, 2026, 1);
        LedgerOps.AddEntry(jan, EntryKind.Recurring, "Gym", 400m, null);
        var feb = LedgerOps.AddMonth(ledger, 2026, 1);
        feb.Entries[0].Amount = 450m;
        LedgerOps.AddEntry(feb, EntryKind.Recurring, "Phone", 300m, null);
        var mar = LedgerOps.AddMonth(ledger, 2026, 1);
        Assert.Equal(["Gym", "Phone"], mar.Entries.Select(e => e.Name));
        Assert.Equal(450m, mar.Entries[0].Amount);
        Assert.Equal(["March 2026", "February 2026", "January 2026"], ledger.Months.Select(m => m.Name));
    }

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

    [Fact]
    public void Rename_rejects_blank_and_trims()
    {
        var ledger = new Ledger();
        var m = LedgerOps.AddMonth(ledger, 2026, 1);
        Assert.False(LedgerOps.RenameMonth(ledger, m.Id, "   "));
        Assert.Equal("January 2026", m.Name);
        Assert.True(LedgerOps.RenameMonth(ledger, m.Id, "  Bonus   month "));
        Assert.Equal("Bonus month", m.Name);
    }

    [Fact]
    public void Deleting_a_category_uncategorises_its_entries()
    {
        var ledger = TestData.Ledger(out var month);
        var food = ledger.Categories.First(c => c.Name == "Food");
        LedgerOps.DeleteCategory(ledger, food.Id);
        Assert.DoesNotContain(month.Entries, e => e.CategoryId == food.Id);
        LedgerJson.Validate(ledger);
    }

    [Fact]
    public void Negative_amounts_are_refused()
    {
        var month = new Month { Id = "m", Name = "M" };
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerOps.AddEntry(month, EntryKind.Expense, "x", -1m, null));
    }

    [Fact]
    public void Duplicate_category_names_are_refused()
    {
        var ledger = new Ledger();
        LedgerOps.AddCategory(ledger, "Food", 0m);
        var ex = Assert.Throws<ArgumentException>(() => LedgerOps.AddCategory(ledger, " food ", 0m));
        Assert.Contains("already", ex.Message);
    }
}
