using System.Globalization;

namespace Ledger.Library.Money;

/// <summary>
/// Reads amounts the way people type them, in either language.
/// </summary>
public static class Amounts
{
    /// <summary>
    /// Reads an amount written with a comma or a dot for the cents, with or without thousands separators and a
    /// currency sign ("9,80", "9.80", "1.960,00", "1,960.00", "1 960", "€12"). The sign is ignored: the amount comes
    /// back positive.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="amount">The amount, rounded to cents.</param>
    /// <returns>True when it is a number.</returns>
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0m;
        var digits = new string((text ?? string.Empty).Where(static character => char.IsAsciiDigit(character) || character is ',' or '.').ToArray());
        if (digits.Length == 0 || !digits.Any(char.IsAsciiDigit))
        {
            return false;
        }

        var lastComma = digits.LastIndexOf(',');
        var lastDot = digits.LastIndexOf('.');
        var decimalAt = Math.Max(lastComma, lastDot);

        // The last separator marks the cents, unless exactly three digits follow it and it is the only kind of
        // separator there: "1.960", "1,960" and "1.234.567" are thousands; "1.960,00" and "0.125" aren't.
        if (decimalAt > 0 && digits.Length - decimalAt - 1 == 3 && digits[..decimalAt] != "0")
        {
            var other = digits[decimalAt] == ',' ? lastDot : lastComma;
            if (other < 0)
            {
                decimalAt = -1;
            }
        }

        var whole = decimalAt < 0 ? digits : digits[..decimalAt];
        var cents = decimalAt < 0 ? string.Empty : digits[(decimalAt + 1)..];
        whole = whole.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        if (whole.Length > 15 || cents.Length > 6)
        {
            return false;
        }

        var invariant = (whole.Length == 0 ? "0" : whole) + (cents.Length == 0 ? string.Empty : "." + cents);
        if (!decimal.TryParse(invariant, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        amount = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        return true;
    }
}
