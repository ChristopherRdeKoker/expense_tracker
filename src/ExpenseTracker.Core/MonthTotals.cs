namespace ExpenseTracker.Core;

public sealed record MonthTotals(decimal Income, decimal Recurring, decimal Expenses)
{
    public decimal Spent => Recurring + Expenses;
    public decimal LeftOver => Income - Spent;
}
