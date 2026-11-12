namespace ExpenseTracker.Core;

public static class NameKey
{
    public static string CollapseSpaces(string? text)
    {
        var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = string.Join(' ', words);

        return result;
    }

    public static string Normalise(string name)
    {
        var result = CollapseSpaces(name).ToLowerInvariant();

        return result;
    }
}
