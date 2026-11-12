namespace ExpenseTracker;

public static class UiText
{
    public const string NoCategory = "No category";

    public static string NotAnAmount(string input) => $"“{input}” is not an amount. Use numbers like 120 or 89.95.";
}
