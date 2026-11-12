using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

[JsonConverter(typeof(JsonStringEnumConverter<EntryKind>))]
public enum EntryKind
{
    Expense,
    Recurring,
    Income
}

public sealed class Category
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public decimal Budget { get; set; }
    public int Color { get; set; }
}

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

public sealed class Month
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    [JsonRequired]
    public List<Entry> Entries { get; set; } = [];
}

public sealed class Ledger
{
    public const int CurrentVersion = 1;

    [JsonRequired]
    public int Version { get; set; } = CurrentVersion;

    [JsonRequired]
    public List<Category> Categories { get; set; } = [];
    [JsonRequired]
    public List<Month> Months { get; set; } = [];

    public Category? FindCategory(string? id) =>
        id is null ? null : Categories.FirstOrDefault(c => c.Id == id);

    public Month? FindMonth(string? id) =>
        id is null ? null : Months.FirstOrDefault(m => m.Id == id);
}
