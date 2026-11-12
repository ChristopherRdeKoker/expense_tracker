using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow)]
[JsonSerializable(typeof(Ledger))]
internal sealed partial class LedgerJsonContext : JsonSerializerContext;

public static class LedgerJson
{
    public static string Serialize(Ledger ledger) =>
        JsonSerializer.Serialize(ledger, LedgerJsonContext.Default.Ledger);

    public static Ledger Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new LedgerFormatException("The data is empty.");
        }

        Ledger? ledger;
        try
        {
            ledger = JsonSerializer.Deserialize(json, LedgerJsonContext.Default.Ledger);
        }
        catch (JsonException ex)
        {
            var where = ex.Path is { Length: > 1 } path ? $" at {path}" : "";
            var line = ex.LineNumber is { } l ? $" (line {l + 1})" : "";
            throw new LedgerFormatException($"This is not valid expense data: the JSON is broken or has the wrong shape{where}{line}.");
        }

        if (ledger is null)
        {
            throw new LedgerFormatException("This is not valid expense data: the file holds no ledger.");
        }

        Validate(ledger);
        return ledger;
    }

    public static void Validate(Ledger ledger)
    {
        if (ledger.Version != Ledger.CurrentVersion)
        {
            throw new LedgerFormatException(ledger.Version > Ledger.CurrentVersion
                ? $"This file was saved by a newer version of the app (format {ledger.Version}); this app reads format {Ledger.CurrentVersion}."
                : $"Unsupported data format version {ledger.Version}.");
        }
        if (ledger.Categories is null || ledger.Months is null)
        {
            throw new LedgerFormatException("This is not valid expense data: \"categories\" and \"months\" must both be lists.");
        }

        var categoryIds = new HashSet<string>();
        for (var i = 0; i < ledger.Categories.Count; i++)
        {
            var c = ledger.Categories[i];
            var label = $"Category {i + 1}";
            if (c is null) throw Fail($"{label} is empty.");
            if (string.IsNullOrWhiteSpace(c.Id)) throw Fail($"{label} has no id.");
            if (!categoryIds.Add(c.Id)) throw Fail($"{label} reuses the id \"{c.Id}\".");
            if (string.IsNullOrWhiteSpace(c.Name)) throw Fail($"{label} has no name.");
            if (c.Name.Length > LedgerOps.MaxNameLength) throw Fail($"{label} has a name longer than {LedgerOps.MaxNameLength} characters.");
            if (LedgerOps.HasCategoryNamed(ledger.Categories.Take(i), c.Name)) throw Fail($"Two categories are called \"{c.Name}\".");
            CheckAmount(c.Budget, $"{label} (\"{c.Name}\") budget");
            if (c.Color < 0 || c.Color >= Palette.Size) throw Fail($"{label} (\"{c.Name}\") has colour {c.Color}; it must be 0 to {Palette.Size - 1}.");
        }

        var monthIds = new HashSet<string>();
        var entryIds = new HashSet<string>();
        for (var m = 0; m < ledger.Months.Count; m++)
        {
            var month = ledger.Months[m];
            var label = $"Month {m + 1}";
            if (month is null) throw Fail($"{label} is empty.");
            if (string.IsNullOrWhiteSpace(month.Id)) throw Fail($"{label} has no id.");
            if (!monthIds.Add(month.Id)) throw Fail($"{label} reuses the id \"{month.Id}\".");
            if (string.IsNullOrWhiteSpace(month.Name)) throw Fail($"{label} has no name.");
            if (month.Name.Length > LedgerOps.MaxNameLength) throw Fail($"{label} has a name longer than {LedgerOps.MaxNameLength} characters.");
            if (month.Entries is null) throw Fail($"{label} (\"{month.Name}\") has no entry list.");
            for (var e = 0; e < month.Entries.Count; e++)
            {
                var entry = month.Entries[e];
                var where = $"Entry {e + 1} in \"{month.Name}\"";
                if (entry is null) throw Fail($"{where} is empty.");
                if (string.IsNullOrWhiteSpace(entry.Id)) throw Fail($"{where} has no id.");
                if (!entryIds.Add(entry.Id)) throw Fail($"{where} reuses the id \"{entry.Id}\".");
                if (entry.Name is null || entry.Name.Length > LedgerOps.MaxNameLength) throw Fail($"{where} has a missing or too-long name.");
                if (!Enum.IsDefined(entry.Kind)) throw Fail($"{where} has an unknown kind.");
                CheckAmount(entry.Amount, $"{where} amount");
                if (entry.CategoryId is not null && !categoryIds.Contains(entry.CategoryId))
                {
                    throw Fail($"{where} (\"{entry.Name}\") points at a category that does not exist (\"{entry.CategoryId}\").");
                }
            }
        }
    }

    private static void CheckAmount(decimal amount, string what)
    {
        if (amount < 0) throw Fail($"{what} is negative ({Money.FormatPlain(amount)}).");
        if (amount > Money.MaxAmount) throw Fail($"{what} is larger than {Money.Format(Money.MaxAmount)}.");
        if (Money.Round(amount) != amount) throw Fail($"{what} has more than two decimals ({amount}).");
    }

    private static LedgerFormatException Fail(string message) =>
        new($"This is not valid expense data: {message}");
}
