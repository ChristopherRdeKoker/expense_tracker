using System.Globalization;
using System.Text;

namespace ExpenseTracker.Core;

public static class Money
{
    public const char GroupSeparator = ' ';
    public const decimal MaxAmount = 1_000_000_000m;

    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static bool IsStorable(decimal amount)
    {
        var isInRange = amount >= 0 && amount <= MaxAmount;
        var hasAtMostTwoDecimals = Round(amount) == amount;
        var result = isInRange && hasAtMostTwoDecimals;

        return result;
    }

    public static string Format(decimal amount)
    {
        var rounded = Round(amount);
        var sign = rounded < 0 ? "-" : "";
        var digits = Math.Abs(rounded).ToString("0.00", CultureInfo.InvariantCulture);
        var point = digits.IndexOf('.');
        var whole = digits[..point];
        var grouped = new StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped.Append(GroupSeparator);
            }
            grouped.Append(whole[i]);
        }
        return $"{sign}R{GroupSeparator}{grouped}{digits[point..]}";
    }

    public static string FormatPlain(decimal amount) =>
        Round(amount).ToString("0.00", CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = new StringBuilder();
        foreach (var ch in text.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch == ' ' || ch == ' ')
            {
                continue;
            }
            cleaned.Append(ch);
        }

        var s = cleaned.ToString();
        if (s.StartsWith('R') || s.StartsWith('r'))
        {
            s = s[1..];
        }

        if (s.Contains(',') && !s.Contains('.'))
        {
            var lastComma = s.LastIndexOf(',');
            var decimals = s.Length - lastComma - 1;
            s = s.Count(c => c == ',') == 1 && decimals is 1 or 2
                ? s.Replace(',', '.')
                : s.Replace(",", "");
        }
        else
        {
            s = s.Replace(",", "");
        }

        if (s.Length == 0 || s.Any(c => !(char.IsAsciiDigit(c) || c == '.')) || s.Count(c => c == '.') > 1)
        {
            return false;
        }

        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (!IsStorable(value))
        {
            return false;
        }

        amount = value;
        return true;
    }
}
