using System.Globalization;

namespace ExpenseTracker.Core;

public static class MonthNames
{
    public const int MaxLookaheadMonths = 120;

    public static string Format(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public static string Normalise(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NextAvailable(IEnumerable<string> existing, int year, int month)
    {
        var taken = existing.Select(Normalise).ToHashSet();
        var cursor = new DateTime(year, month, 1);
        for (var i = 0; i < MaxLookaheadMonths; i++)
        {
            var candidate = Format(cursor.Year, cursor.Month);
            if (!taken.Contains(Normalise(candidate)))
            {
                return candidate;
            }
            cursor = cursor.AddMonths(1);
        }
        throw new InvalidOperationException(
            $"Could not find a free month name: all {MaxLookaheadMonths} months from {Format(year, month)} are already taken.");
    }
}
