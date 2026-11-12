using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

[JsonConverter(typeof(JsonStringEnumConverter<EntryKind>))]
public enum EntryKind
{
    Expense,
    Recurring,
    Income
}
