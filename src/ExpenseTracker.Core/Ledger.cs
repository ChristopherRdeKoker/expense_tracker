using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

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
