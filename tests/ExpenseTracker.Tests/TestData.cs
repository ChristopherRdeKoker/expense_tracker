using System.Globalization;
using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

internal static class TestData
{
    public static Ledger Ledger(out Month month)
    {
        var ledger = new Ledger();
        var food = LedgerOps.AddCategory(ledger, "Food", 1000m);
        var rent = LedgerOps.AddCategory(ledger, "Rent", 5000m);
        LedgerOps.AddCategory(ledger, "Unbudgeted", 0m);
        month = LedgerOps.AddMonth(ledger, 2026, 3);
        LedgerOps.AddEntry(month, EntryKind.Income, "Salary", 20000m, null);
        LedgerOps.AddEntry(month, EntryKind.Recurring, "Rent", 5000m, rent.Id);
        LedgerOps.AddEntry(month, EntryKind.Expense, "Shop", 600.25m, food.Id);
        LedgerOps.AddEntry(month, EntryKind.Expense, "Takeaway", 300.50m, food.Id);
        LedgerOps.AddEntry(month, EntryKind.Expense, "Mystery", 99.25m, null);
        return ledger;
    }

    public static decimal Dec(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
}
