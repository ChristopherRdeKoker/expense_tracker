using System.Globalization;
using System.Text;

namespace ExpenseTracker.Core;

public sealed record CsvRow(string Month, EntryKind Kind, string Name, string Category, decimal Amount);

public static class LedgerCsv
{
    public const string Header = "Month,Kind,Name,Category,Amount";
    public const string FormulaLeadChars = "=+-@\t\r";

    public static string Export(Ledger ledger)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append("\r\n");
        foreach (var month in ledger.Months)
        {
            foreach (var e in month.Entries)
            {
                sb.Append(Field(month.Name)).Append(',')
                  .Append(e.Kind).Append(',')
                  .Append(Field(e.Name)).Append(',')
                  .Append(Field(ledger.FindCategory(e.CategoryId)?.Name ?? "")).Append(',')
                  .Append(Money.FormatPlain(e.Amount)).Append("\r\n");
            }
        }
        return sb.ToString();
    }

    public static IReadOnlyList<CsvRow> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new LedgerFormatException("The CSV file is empty.");
        }

        var records = ReadRecords(csv.TrimStart('﻿'));
        if (records.Count == 0 || string.Join(',', records[0]).Trim() != Header)
        {
            throw new LedgerFormatException($"This is not an expense CSV: the first line must be \"{Header}\".");
        }

        var rows = new List<CsvRow>();
        for (var i = 1; i < records.Count; i++)
        {
            var r = records[i];
            var line = i + 1;
            if (r.Count == 1 && r[0].Length == 0)
            {
                continue;
            }
            if (r.Count != 5)
            {
                throw new LedgerFormatException($"CSV row {line} has {r.Count} columns; expected 5.");
            }
            if (!Enum.TryParse<EntryKind>(r[1], ignoreCase: true, out var kind) || !Enum.IsDefined(kind) || int.TryParse(r[1], out _))
            {
                throw new LedgerFormatException($"CSV row {line}: unknown kind \"{r[1]}\" (use Expense, Recurring or Income).");
            }
            if (!decimal.TryParse(r[4], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
                || !Money.IsStorable(amount))
            {
                throw new LedgerFormatException($"CSV row {line}: \"{r[4]}\" is not a valid amount.");
            }
            if (string.IsNullOrWhiteSpace(r[0]))
            {
                throw new LedgerFormatException($"CSV row {line} has no month.");
            }
            rows.Add(new CsvRow(r[0], kind, Unguard(r[2]), Unguard(r[3]), amount));
        }
        return rows;
    }

    public static Ledger ToLedger(IReadOnlyList<CsvRow> rows, IEnumerable<Category>? keepBudgetsFrom = null)
    {
        var ledger = new Ledger();
        foreach (var known in keepBudgetsFrom ?? [])
        {
            ledger.Categories.Add(new Category { Id = known.Id, Name = known.Name, Budget = known.Budget, Color = known.Color });
        }

        var months = new Dictionary<string, Month>();
        foreach (var row in rows)
        {
            var monthKey = NameKey.Normalise(row.Month);
            if (!months.TryGetValue(monthKey, out var month))
            {
                month = new Month { Id = Ids.New(), Name = LedgerOps.CleanName(row.Month) };
                months[monthKey] = month;
                ledger.Months.Add(month);
            }

            string? categoryId = null;
            if (row.Kind != EntryKind.Income && !string.IsNullOrWhiteSpace(row.Category))
            {
                var key = NameKey.Normalise(row.Category);
                categoryId = (ledger.Categories.FirstOrDefault(c => NameKey.Normalise(c.Name) == key)
                              ?? LedgerOps.AddCategory(ledger, row.Category, 0m)).Id;
            }
            LedgerOps.AddEntry(month, row.Kind, row.Name, row.Amount, categoryId);
        }
        LedgerJson.Validate(ledger);
        return ledger;
    }

    private static string Field(string value)
    {
        if (value.Length > 0 && FormulaLeadChars.Contains(value[0]))
        {
            value = "'" + value;
        }
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static string Unguard(string value) =>
        value.Length > 1 && value[0] == '\'' && FormulaLeadChars.Contains(value[1]) ? value[1..] : value;

    private static List<List<string>> ReadRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
                i++;
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r' or '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = [];
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    break;
                default:
                    field.Append(c);
                    break;
            }
            i++;
        }

        if (inQuotes)
        {
            throw new LedgerFormatException("The CSV file ends inside a quoted field.");
        }
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
