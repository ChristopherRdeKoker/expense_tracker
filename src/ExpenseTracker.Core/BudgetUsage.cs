namespace ExpenseTracker.Core;

public sealed record BudgetUsage(Category Category, decimal Spent, decimal Budget)
{
    public const decimal NearThreshold = 85m;

    public decimal? Percent => Budget > 0 ? Math.Round(Spent / Budget * 100m, 1, MidpointRounding.AwayFromZero) : null;

    public decimal Remaining => Budget - Spent;

    public decimal BarFraction => Budget > 0 ? Math.Min(1m, Spent / Budget) : 0m;

    public BudgetState State => Budget switch
    {
        <= 0m => BudgetState.NoBudget,
        _ when Spent > Budget => BudgetState.Over,
        _ when Spent * 100m >= NearThreshold * Budget => BudgetState.Near,
        _ => BudgetState.Under
    };
}
