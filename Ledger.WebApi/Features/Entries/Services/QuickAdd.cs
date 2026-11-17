using System.Text.RegularExpressions;
using Ledger.Library.Money;

namespace Ledger.WebApi.Features.Entries.Services;

/// <summary>
/// Reads a quick-add line: an amount and a few words, in either order ("9.80 lunch", "lunch 9,80", "+1960 salary").
/// </summary>
internal static partial class QuickAdd
{
    /// <summary>
    /// Splits a line into its amount and its words.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>Whether an amount was found, the amount, the words and whether a plus sign marked it as an inflow.</returns>
    public static (bool Understood, decimal Amount, string Name, bool Plus) Parse(string? text)
    {
        var line = (text ?? string.Empty).Trim();
        var match = AmountPattern().Match(line);
        if (!match.Success || !TryAmount(match.Groups["number"].Value, out var amount) || amount <= 0)
        {
            return (false, 0m, line, false);
        }

        var name = (line[..match.Index] + " " + line[(match.Index + match.Length)..]).Trim();
        name = Spaces().Replace(name, " ");
        return (true, amount, name, match.Groups["sign"].Value == "+");
    }

    /// <summary>Reads an amount the way people type it (see <see cref="Amounts.TryParse"/>).</summary>
    /// <param name="text">The text.</param>
    /// <param name="amount">The amount, rounded to cents.</param>
    /// <returns>True when it is a number.</returns>
    public static bool TryAmount(string? text, out decimal amount) => Amounts.TryParse(text, out amount);

    [GeneratedRegex(@"(?<![\p{L}\d])(?<sign>[+\-−]?)\s*€?\s*(?<number>\d[\d.,\s]*\d|\d)\s*€?(?![\p{L}])")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
