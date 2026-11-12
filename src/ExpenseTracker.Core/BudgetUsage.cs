namespace ExpenseTracker.Core;

public sealed record BudgetUsage(Category Category, decimal Spent, decimal Budget)
{
    public const decimal NearThreshold = 85m;

    public decimal? Percent => Budget > 0 ? Math.Round(Spent / Budget * 100m, 1, MidpointRounding.AwayFromZero) : null;

    public decimal Remaining => Budget - Spent;

    public decimal BarFraction => Budget > 0 ? Math.Min(1m, Spent / Budget) : 0m;

    public BudgetState State => Percent switch
    {
        null => BudgetState.NoBudget,
        > 100m => BudgetState.Over,
        >= NearThreshold => BudgetState.Near,
        _ => BudgetState.Under
    };
}
