using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

public sealed class Month
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    [JsonRequired]
    public List<Entry> Entries { get; set; } = [];
}
