namespace ExpenseTracker.Core;

public sealed record CsvRow(string Month, EntryKind Kind, string Name, string Category, decimal Amount);
