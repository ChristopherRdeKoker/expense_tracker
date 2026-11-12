namespace ExpenseTracker.Core;

public static class LedgerMath
{
    public static MonthTotals Totals(IEnumerable<Entry> entries)
    {
        decimal income = 0, recurring = 0, expenses = 0;
        foreach (var e in entries)
        {
            switch (e.Kind)
            {
                case EntryKind.Income: income += e.Amount; break;
                case EntryKind.Recurring: recurring += e.Amount; break;
                default: expenses += e.Amount; break;
            }
        }
        return new MonthTotals(income, recurring, expenses);
    }

    public static decimal SpentIn(IEnumerable<Entry> entries, string categoryId) =>
        entries.Where(e => e.IsSpending && e.CategoryId == categoryId).Sum(e => e.Amount);

    public static IReadOnlyList<BudgetUsage> Budgets(Ledger ledger, Month month) =>
        ledger.Categories
            .Select(c => new BudgetUsage(c, SpentIn(month.Entries, c.Id), c.Budget))
            .ToList();

    public static BudgetUsage? Overall(Ledger ledger, Month month)
    {
        var budgeted = ledger.Categories.Where(c => c.Budget > 0).ToList();
        if (budgeted.Count == 0)
        {
            return null;
        }
        var spent = budgeted.Sum(c => SpentIn(month.Entries, c.Id));
        var pseudo = new Category { Id = "", Name = "All budgets", Budget = budgeted.Sum(c => c.Budget) };
        return new BudgetUsage(pseudo, spent, pseudo.Budget);
    }

    public static IReadOnlyList<CategorySlice> Breakdown(Ledger ledger, Month month)
    {
        var spending = month.Entries.Where(e => e.IsSpending && e.Amount > 0).ToList();
        var total = spending.Sum(e => e.Amount);
        if (total == 0)
        {
            return [];
        }

        return spending
            .GroupBy(e => ledger.FindCategory(e.CategoryId)?.Id)
            .Select(g => new CategorySlice(ledger.FindCategory(g.Key), g.Sum(e => e.Amount), g.Sum(e => e.Amount) / total))
            .OrderByDescending(s => s.Amount)
            .ToList();
    }
}
