namespace ExpenseTracker.Core;

public sealed class Entry
{
    public required string Id { get; set; }
    public required EntryKind Kind { get; set; }
    public required string Name { get; set; }
    public required decimal Amount { get; set; }
    public string? CategoryId { get; set; }

    public bool IsSpending => Kind != EntryKind.Income;

    public Entry Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        Name = Name,
        Amount = Amount,
        CategoryId = CategoryId
    };
}
