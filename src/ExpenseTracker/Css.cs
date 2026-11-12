using System.Globalization;
using ExpenseTracker.Core;

namespace ExpenseTracker;

public static class Css
{
    public static string Color(Category? category) => category is null ? "cat-none" : $"cat-{category.Color}";

    public static string Percent(decimal fraction) =>
        (Math.Clamp(fraction, 0m, 1m) * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string Tone(EntryKind kind) => kind == EntryKind.Income ? "tone-good" : "";

    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string State(BudgetState state) => state switch
    {
        BudgetState.Over => "is-over",
        BudgetState.Near => "is-near",
        BudgetState.Under => "is-under",
        _ => "is-none"
    };
}
