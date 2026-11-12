namespace ExpenseTracker.Core;

public sealed class Category
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public decimal Budget { get; set; }
    public int Color { get; set; }
}
