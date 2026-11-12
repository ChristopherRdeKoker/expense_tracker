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
    public void Recurring_3500_plus_699_carry_as_4199_and_nothing_else_does()
    {
        var ledger = new Ledger();
        var november = LedgerOps.AddMonth(ledger, 2026, 11);
        LedgerOps.AddEntry(november, EntryKind.Income, "Salary", 20_000m, null);
        LedgerOps.AddEntry(november, EntryKind.Recurring, "Rent", 3_500m, null);
        LedgerOps.AddEntry(november, EntryKind.Recurring, "Fibre", 699m, null);
        LedgerOps.AddEntry(november, EntryKind.Expense, "Shop", 100m, null);

        var december = LedgerOps.AddMonth(ledger, 2026, 11);
        var totals = LedgerMath.Totals(december.Entries);

        Assert.Equal("December 2026", december.Name);
        Assert.Equal(0m, totals.Income);
        Assert.Equal(4_199m, totals.Recurring);
        Assert.Equal(0m, totals.Expenses);
        Assert.Equal(-4_199m, totals.LeftOver);
        Assert.Equal(20_000m - 3_500m - 699m - 100m, LedgerMath.Totals(november.Entries).LeftOver);
    }

    [Fact]
    public void Recurring_amounts_with_cents_carry_unchanged_across_three_months()
    {
        var ledger = new Ledger();
        var first = LedgerOps.AddMonth(ledger, 2026, 11);
        LedgerOps.AddEntry(first, EntryKind.Recurring, "Gym", 449.99m, null);
        LedgerOps.AddEntry(first, EntryKind.Recurring, "Phone", 0.01m, null);

        LedgerOps.AddMonth(ledger, 2026, 11);
        var third = LedgerOps.AddMonth(ledger, 2026, 11);

        Assert.Equal("January 2027", third.Name);
        Assert.Equal(450.00m, LedgerMath.Totals(third.Entries).Recurring);
    }
}
