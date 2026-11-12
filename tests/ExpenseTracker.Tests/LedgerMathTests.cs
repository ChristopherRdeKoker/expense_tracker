using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class LedgerMathTests
{
    [Fact]
    public void Totals_split_income_recurring_and_expenses()
    {
        TestData.Ledger(out var month);
        var t = LedgerMath.Totals(month.Entries);
        Assert.Equal(20000m, t.Income);
        Assert.Equal(5000m, t.Recurring);
        Assert.Equal(1000m, t.Expenses);
        Assert.Equal(6000m, t.Spent);
        Assert.Equal(14000m, t.LeftOver);
    }

    [Fact]
    public void Left_over_goes_negative_when_overspent()
    {
        var month = new Month { Id = "m", Name = "M" };
        LedgerOps.AddEntry(month, EntryKind.Income, "Pay", 100m, null);
        LedgerOps.AddEntry(month, EntryKind.Expense, "Big", 250.10m, null);
        Assert.Equal(-150.10m, LedgerMath.Totals(month.Entries).LeftOver);
    }

    [Fact]
    public void Totals_of_nothing_are_zero()
    {
        var t = LedgerMath.Totals([]);
        Assert.Equal(0m, t.Income);
        Assert.Equal(0m, t.LeftOver);
    }

    [Fact]
    public void Budget_percent_and_state()
    {
        var ledger = TestData.Ledger(out var month);
        var usage = LedgerMath.Budgets(ledger, month).ToDictionary(u => u.Category.Name);

        Assert.Equal(900.75m, usage["Food"].Spent);
        Assert.Equal(90.1m, usage["Food"].Percent);
        Assert.Equal(BudgetState.Near, usage["Food"].State);
        Assert.Equal(99.25m, usage["Food"].Remaining);

        Assert.Equal(100m, usage["Rent"].Percent);
        Assert.Equal(BudgetState.Near, usage["Rent"].State);

        Assert.Null(usage["Unbudgeted"].Percent);
        Assert.Equal(BudgetState.NoBudget, usage["Unbudgeted"].State);
        Assert.Equal(0m, usage["Unbudgeted"].BarFraction);
    }

    [Fact]
    public void Budget_over_100_percent_is_over_and_bar_is_capped()
    {
        var ledger = TestData.Ledger(out var month);
        var food = ledger.Categories.First(c => c.Name == "Food");
        LedgerOps.AddEntry(month, EntryKind.Expense, "Feast", 600m, food.Id);
        var usage = LedgerMath.Budgets(ledger, month).First(u => u.Category.Id == food.Id);
        Assert.Equal(150.1m, usage.Percent);
        Assert.Equal(BudgetState.Over, usage.State);
        Assert.Equal(1m, usage.BarFraction);
        Assert.Equal(-500.75m, usage.Remaining);
    }

    [Fact]
    public void Budget_under_threshold_is_under()
    {
        var u = new BudgetUsage(new Category { Id = "c", Name = "C", Budget = 100m }, 84.9m, 100m);
        Assert.Equal(BudgetState.Under, u.State);
    }

    [Fact]
    public void Income_never_counts_against_a_budget()
    {
        var ledger = TestData.Ledger(out var month);
        var food = ledger.Categories.First(c => c.Name == "Food");
        LedgerOps.AddEntry(month, EntryKind.Income, "Refund", 500m, food.Id);
        Assert.Equal(900.75m, LedgerMath.SpentIn(month.Entries, food.Id));
    }

    [Fact]
    public void Overall_budget_only_counts_budgeted_categories()
    {
        var ledger = TestData.Ledger(out var month);
        var overall = LedgerMath.Overall(ledger, month)!;
        Assert.Equal(6000m, overall.Budget);
        Assert.Equal(5900.75m, overall.Spent);
    }

    [Fact]
    public void Overall_is_null_without_any_budget()
    {
        var ledger = new Ledger();
        LedgerOps.AddCategory(ledger, "A", 0m);
        var month = LedgerOps.AddMonth(ledger, 2026, 1);
        Assert.Null(LedgerMath.Overall(ledger, month));
    }

    [Fact]
    public void Breakdown_sums_to_one_and_keeps_uncategorised()
    {
        var ledger = TestData.Ledger(out var month);
        var slices = LedgerMath.Breakdown(ledger, month);
        Assert.Equal(3, slices.Count);
        Assert.Equal("Rent", slices[0].Category!.Name);
        Assert.Contains(slices, s => s.Category is null && s.Amount == 99.25m);
        Assert.Equal(1m, Math.Round(slices.Sum(s => s.Fraction), 10));
    }

    [Fact]
    public void Breakdown_of_income_only_month_is_empty()
    {
        var ledger = new Ledger();
        var month = LedgerOps.AddMonth(ledger, 2026, 1);
        LedgerOps.AddEntry(month, EntryKind.Income, "Pay", 10m, null);
        Assert.Empty(LedgerMath.Breakdown(ledger, month));
    }
}
