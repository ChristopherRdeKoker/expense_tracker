namespace ExpenseTracker.Core;

public static class Seed
{
    public static Ledger Empty(int year, int month)
    {
        var ledger = new Ledger();
        foreach (var (name, budget) in DefaultCategories)
        {
            LedgerOps.AddCategory(ledger, name, budget);
        }
        LedgerOps.AddMonth(ledger, year, month);
        return ledger;
    }

    public static Ledger Sample(int year, int month)
    {
        var ledger = Empty(year, month);
        var current = ledger.Months[0];
        string Cat(string name) => ledger.Categories.First(c => c.Name == name).Id;

        LedgerOps.AddEntry(current, EntryKind.Income, "Salary", 32_500m, null);
        LedgerOps.AddEntry(current, EntryKind.Recurring, "Rent", 9_800m, Cat("Housing"));
        LedgerOps.AddEntry(current, EntryKind.Recurring, "Electricity & water", 1_450m, Cat("Utilities"));
        LedgerOps.AddEntry(current, EntryKind.Recurring, "Fibre", 699m, Cat("Utilities"));
        LedgerOps.AddEntry(current, EntryKind.Recurring, "Medical aid", 2_180m, Cat("Health"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Woolies weekly shop", 1_284.35m, Cat("Groceries"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Checkers", 912.60m, Cat("Groceries"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Petrol", 1_150m, Cat("Transport"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Gautrain card", 420m, Cat("Transport"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Sushi Friday", 685.50m, Cat("Eating out"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Coffee", 186m, Cat("Eating out"));
        LedgerOps.AddEntry(current, EntryKind.Expense, "Cinema", 240m, Cat("Fun"));
        return ledger;
    }

    public static readonly (string Name, decimal Budget)[] DefaultCategories =
    [
        ("Groceries", 4_000m),
        ("Housing", 10_000m),
        ("Transport", 2_500m),
        ("Utilities", 2_200m),
        ("Eating out", 1_200m),
        ("Health", 2_500m),
        ("Fun", 800m),
        ("Other", 0m)
    ];
}
