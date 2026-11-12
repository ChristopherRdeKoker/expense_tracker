namespace ExpenseTracker.Core;

public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N")[..12];
}
