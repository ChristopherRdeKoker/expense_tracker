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

    private static Month MonthOf(params (EntryKind Kind, decimal Amount, string? CategoryId)[] rows)
    {
        var month = new Month { Id = "m", Name = "M" };
        foreach (var (kind, amount, categoryId) in rows)
        {
            LedgerOps.AddEntry(month, kind, "x", amount, categoryId);
        }

        return month;
    }

    private static BudgetUsage Usage(decimal spent, decimal budget) =>
        new(new Category { Id = "c", Name = "C", Budget = budget }, spent, budget);

    [Fact]
    public void Income_10000_less_recurring_3500_less_expenses_1000_50_and_250_25_leaves_5249_25()
    {
        var month = MonthOf(
            (EntryKind.Income, 10_000m, null),
            (EntryKind.Recurring, 3_500m, null),
            (EntryKind.Expense, 1_000.50m, null),
            (EntryKind.Expense, 250.25m, null));

        var totals = LedgerMath.Totals(month.Entries);

        Assert.Equal(1_250.75m, totals.Expenses);
        Assert.Equal(4_750.75m, totals.Spent);
        Assert.Equal(5_249.25m, totals.LeftOver);
    }

    [Fact]
    public void Cents_0_10_plus_0_20_total_exactly_0_30()
    {
        var month = MonthOf((EntryKind.Expense, 0.10m, null), (EntryKind.Expense, 0.20m, null));

        Assert.Equal(0.30m, LedgerMath.Totals(month.Entries).Expenses);
    }

    [Fact]
    public void Three_incomes_of_one_billion_total_3_billion_past_the_per_entry_cap()
    {
        const decimal threeBillion = 3 * Money.MaxAmount;
        var month = MonthOf(
            (EntryKind.Income, Money.MaxAmount, null),
            (EntryKind.Income, Money.MaxAmount, null),
            (EntryKind.Income, Money.MaxAmount, null));

        Assert.Equal(threeBillion, LedgerMath.Totals(month.Entries).Income);
    }

    [Fact]
    public void Income_0_01_less_two_billion_spent_leaves_minus_1_999_999_999_99()
    {
        var month = MonthOf(
            (EntryKind.Income, 0.01m, null),
            (EntryKind.Expense, Money.MaxAmount, null),
            (EntryKind.Recurring, Money.MaxAmount, null));

        Assert.Equal(-1_999_999_999.99m, LedgerMath.Totals(month.Entries).LeftOver);
    }

    [Fact]
    public void Zero_amount_entries_add_nothing()
    {
        var month = MonthOf((EntryKind.Income, 0m, null), (EntryKind.Expense, 0m, null));
        var totals = LedgerMath.Totals(month.Entries);

        Assert.Equal(0m, totals.Spent);
        Assert.Equal(0m, totals.LeftOver);
    }

    [Theory]
    [InlineData("1", "3", "33.3")]
    [InlineData("2", "3", "66.7")]
    [InlineData("0.05", "100", "0.1")]
    [InlineData("0.04", "100", "0.0")]
    [InlineData("1234.56", "1000", "123.5")]
    [InlineData("0", "500", "0.0")]
    public void Percent_is_spent_over_budget_rounded_to_one_decimal_half_away_from_zero(string spent, string budget, string percent)
    {
        Assert.Equal(TestData.Dec(percent), Usage(TestData.Dec(spent), TestData.Dec(budget)).Percent);
    }

    [Theory]
    [InlineData("849.99", "1000", BudgetState.Near)]
    [InlineData("849.49", "1000", BudgetState.Under)]
    [InlineData("850", "1000", BudgetState.Near)]
    [InlineData("1000", "1000", BudgetState.Near)]
    [InlineData("1001", "1000", BudgetState.Over)]
    [InlineData("1000.01", "1000", BudgetState.Near)]
    [InlineData("5", "0", BudgetState.NoBudget)]
    public void State_follows_the_rounded_percent_85_near_and_above_100_over(string spent, string budget, BudgetState state)
    {
        Assert.Equal(state, Usage(TestData.Dec(spent), TestData.Dec(budget)).State);
    }

    [Theory]
    [InlineData("250", "1000", "0.25")]
    [InlineData("1234.56", "1000", "1")]
    [InlineData("0", "1000", "0")]
    [InlineData("10", "0", "0")]
    public void Bar_fraction_is_spent_over_budget_capped_at_one(string spent, string budget, string fraction)
    {
        Assert.Equal(TestData.Dec(fraction), Usage(TestData.Dec(spent), TestData.Dec(budget)).BarFraction);
    }

    [Fact]
    public void Budget_1000_less_spent_1234_56_remains_minus_234_56()
    {
        Assert.Equal(-234.56m, Usage(1_234.56m, 1_000m).Remaining);
    }

    [Fact]
    public void Overall_is_300_plus_600_spent_of_1000_plus_500_budget_ignoring_unbudgeted_999()
    {
        var ledger = new Ledger();
        var food = LedgerOps.AddCategory(ledger, "Food", 1_000m);
        var fuel = LedgerOps.AddCategory(ledger, "Fuel", 500m);
        var other = LedgerOps.AddCategory(ledger, "Other", 0m);
        var month = LedgerOps.AddMonth(ledger, 2026, 1);
        LedgerOps.AddEntry(month, EntryKind.Expense, "a", 300m, food.Id);
        LedgerOps.AddEntry(month, EntryKind.Recurring, "b", 600m, fuel.Id);
        LedgerOps.AddEntry(month, EntryKind.Expense, "c", 999m, other.Id);

        var overall = LedgerMath.Overall(ledger, month)!;

        Assert.Equal(900m, overall.Spent);
        Assert.Equal(1_500m, overall.Budget);
        Assert.Equal(60.0m, overall.Percent);
    }

    [Fact]
    public void Breakdown_of_100_and_200_and_300_is_one_sixth_two_sixths_three_sixths()
    {
        var ledger = new Ledger();
        var a = LedgerOps.AddCategory(ledger, "A", 0m);
        var b = LedgerOps.AddCategory(ledger, "B", 0m);
        var month = LedgerOps.AddMonth(ledger, 2026, 1);
        LedgerOps.AddEntry(month, EntryKind.Expense, "x", 100m, a.Id);
        LedgerOps.AddEntry(month, EntryKind.Recurring, "y", 200m, b.Id);
        LedgerOps.AddEntry(month, EntryKind.Expense, "z", 300m, null);
        LedgerOps.AddEntry(month, EntryKind.Income, "pay", 5_000m, null);

        var slices = LedgerMath.Breakdown(ledger, month);

        Assert.Equal([300m, 200m, 100m], slices.Select(s => s.Amount));
        Assert.Equal(300m / 600m, slices[0].Fraction);
        Assert.Equal(200m / 600m, slices[1].Fraction);
        Assert.Equal(100m / 600m, slices[2].Fraction);
        Assert.Null(slices[0].Category);
    }
}
