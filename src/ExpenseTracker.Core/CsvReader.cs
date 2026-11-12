using System.Text;

namespace ExpenseTracker.Core;

internal static class CsvReader
{
    public static List<List<string>> ReadRecords(string text)
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
