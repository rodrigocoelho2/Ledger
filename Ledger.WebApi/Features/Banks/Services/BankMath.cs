using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Banks.Http;
using Ledger.WebApi.Features.Entries.Models.Entities;

namespace Ledger.WebApi.Features.Banks.Services;

/// <summary>A bank transaction read the way Ledger needs it.</summary>
/// <param name="ExternalId">Ledger's id for it (unique per person).</param>
/// <param name="Kind">Inflow or outflow.</param>
/// <param name="Amount">The amount, positive.</param>
/// <param name="Day">The day it was booked.</param>
/// <param name="Name">A name for the entry.</param>
/// <param name="Text">The bank's description, cleaned (a second source for guessing the category).</param>
internal sealed record BankMove(string ExternalId, EntryKind Kind, decimal Amount, DateOnly Day, string Name, string Text = "");

/// <summary>
/// The rules of bringing bank transactions in: ids, names, signs, matching what was already written by hand, and the
/// opening balance that makes Ledger agree with the bank.
/// </summary>
internal static partial class BankMath
{
    /// <summary>How many days apart a hand-written entry and its bank transaction can be.</summary>
    public const int MatchDays = 3;

    /// <summary>The longest external id (dbo.Entry.ExternalID).</summary>
    public const int ExternalIdLength = 100;

    /// <summary>The longest entry name.</summary>
    public const int NameLength = 120;

    /// <summary>Short names that stay in capitals after Title Case (company forms, Portuguese brands, payment schemes).</summary>
    private static readonly HashSet<string> Capitals = new(StringComparer.OrdinalIgnoreCase)
    {
        "SA", "MB", "EDP", "CGD", "BCP", "BPI", "CTT", "NOS", "MEO", "CP", "BP", "EMEL", "EPAL", "IKEA", "IRS", "IMI", "IUC", "AT", "SEPA", "ATM", "TPA", "UK", "EU", "PT",
    };

    /// <summary>Towns a card terminal glues to the shop's name (the bank writes them without accents).</summary>
    private static readonly string[] Towns =
    [
        "lisboa", "loures", "odivelas", "amadora", "oeiras", "alges", "setubal", "faro", "porto", "almada", "sintra",
        "cascais", "cacem", "moscavide", "sacavem", "vilnius", "hilversum", "cork", "amsterdam", "brooklyn", "dublin",
    ];

    /// <summary>The balance kinds, the most useful first: booked at close, booked now, expected, then available.</summary>
    private static readonly string[] BalanceOrder = ["CLBD", "ITBD", "XPCD", "OPBD", "CLAV", "ITAV", "OPAV"];

    /// <summary>The prefix of a link's ids, so two connections never share one.</summary>
    /// <param name="linkId">The link.</param>
    /// <returns>Eight characters and a colon.</returns>
    public static string Prefix(Guid linkId) => linkId.ToString("N")[..8] + ":";

    /// <summary>An id without its link's prefix: the bank's own id, the same after the account is connected again.</summary>
    /// <param name="externalId">Ledger's id for a move.</param>
    /// <returns>The id the bank gave it.</returns>
    public static string Bare(string externalId) => externalId.Length > 9 && externalId[8] == ':' ? externalId[9..] : externalId;

    /// <summary>
    /// The bank's own ids of what one account already holds from any connection, past ones included: disconnecting and
    /// connecting again gives the link a new prefix, but the bank still calls each move the same.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="entries">The entries around the days asked for.</param>
    /// <returns>The ids, without prefix.</returns>
    public static HashSet<string> Held(Guid accountId, IEnumerable<EntryRow> entries)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.ExternalID is { } leaving && entry.AccountUniqueID == accountId)
            {
                held.Add(Bare(leaving));
            }

            if (entry.ToExternalID is { } arriving && entry.ToAccountUniqueID == accountId)
            {
                held.Add(Bare(arriving));
            }
        }

        return held;
    }

    /// <summary>Reads the transactions of a link, oldest first, skipping what isn't booked or has no amount.</summary>
    /// <param name="linkId">The link (for the ids).</param>
    /// <param name="transactions">What the bank sent.</param>
    /// <param name="today">Today, for a transaction without a day.</param>
    /// <returns>The moves.</returns>
    public static List<BankMove> Read(Guid linkId, IEnumerable<EbTransaction> transactions, DateOnly today)
    {
        var moves = new List<BankMove>();
        var repeats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var transaction in transactions)
        {
            if (transaction.Status is { } status && !status.Equals("BOOK", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!decimal.TryParse(transaction.TransactionAmount?.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount == 0m)
            {
                continue;
            }

            var kind = transaction.CreditDebitIndicator?.ToUpperInvariant() switch
            {
                "CRDT" => EntryKind.Inflow,
                "DBIT" => EntryKind.Outflow,
                _ => amount > 0 ? EntryKind.Inflow : EntryKind.Outflow,
            };

            var day = Day(transaction.BookingDate) ?? Day(transaction.ValueDate) ?? Day(transaction.TransactionDate) ?? today;
            var id = transaction.EntryReference is { Length: > 0 } reference ? reference
                : transaction.TransactionId is { Length: > 0 } transactionId ? transactionId
                : "h" + Hash(transaction, day, amount);

            // The same id twice in one answer (a bank that reuses references): the second gets a counter.
            var count = repeats.TryGetValue(id, out var seen) ? seen + 1 : 0;
            repeats[id] = count;
            if (count > 0)
            {
                id += "#" + count.ToString(CultureInfo.InvariantCulture);
            }

            var external = Prefix(linkId) + id;
            if (external.Length > ExternalIdLength)
            {
                external = Prefix(linkId) + "h" + Sha(id);
            }

            moves.Add(new BankMove(external, kind, Math.Abs(decimal.Round(amount, 2, MidpointRounding.AwayFromZero)), day, Name(transaction, kind), Clean(string.Join(' ', transaction.RemittanceInformation ?? []))));
        }

        return [.. moves.OrderBy(static move => move.Day)];
    }

    /// <summary>A name for the entry: who paid or was paid, else the description, cleaned.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="kind">Inflow or outflow.</param>
    /// <returns>The name (never empty).</returns>
    public static string Name(EbTransaction transaction, EntryKind kind)
    {
        var party = kind == EntryKind.Inflow ? transaction.Debtor?.Name : transaction.Creditor?.Name;
        var text = Clean(party);
        if (text.Length == 0)
        {
            text = Clean(string.Join(' ', transaction.RemittanceInformation ?? []));
        }

        if (text.Length == 0)
        {
            text = kind == EntryKind.Inflow ? "Bank credit" : "Bank debit";
        }

        return text.Length > NameLength ? text[..NameLength].TrimEnd() : text;
    }

    /// <summary>
    /// Cleans a bank description: SEPA field tags, card-purchase prefixes and card numbers go, spaces are squeezed, and
    /// SHOUTING becomes Title Case.
    /// </summary>
    /// <param name="text">The description.</param>
    /// <returns>The clean text (empty when nothing is left).</returns>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = SepaTags().Replace(text, " ");
        clean = CardPrefix().Replace(clean, string.Empty);

        // A card purchase as Crédito Agrícola writes it: the shop cut short, the terminal, then the shop with its town
        // glued on ("Pingo Docelisb 8373992 Pingo Docelisboa"). Only the shop is kept: no post code, no town.
        if (Terminal().Match(clean) is { Success: true } card)
        {
            var refund = clean.StartsWith("Dev ", StringComparison.OrdinalIgnoreCase);
            clean = PostCode().Replace(card.Groups["shop"].Value, string.Empty);
            clean = Untown(TrailingDigits().Replace(clean.TrimEnd(), string.Empty));
            clean = refund ? "Devolução " + clean : clean;
        }

        clean = Spaces().Replace(clean, " ").Trim(' ', '-', '/', '.', ',', ';', ':', '*', '#');
        if (clean.Any(char.IsLetter) && !clean.Any(char.IsLower))
        {
            clean = CultureInfo.GetCultureInfo("pt-PT").TextInfo.ToTitleCase(clean.ToLowerInvariant());
            clean = string.Join(' ', clean.Split(' ').Select(static word => Capitals.Contains(word) ? word.ToUpperInvariant() : word));
        }

        return clean;
    }

    /// <summary>The balance the bank would show: booked first, then available.</summary>
    /// <param name="balances">What the bank sent.</param>
    /// <returns>The balance, or null when there is none.</returns>
    public static decimal? Balance(IEnumerable<EbBalance> balances)
    {
        var list = balances.Where(static balance => balance.BalanceAmount is not null).ToList();
        var best = list
            .OrderBy(static balance => Array.IndexOf(BalanceOrder, (balance.BalanceType ?? string.Empty).ToUpperInvariant()) is var index and >= 0 ? index : BalanceOrder.Length)
            .FirstOrDefault();
        return best is not null && decimal.TryParse(best.BalanceAmount!.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    /// <summary>
    /// The hand-written entry a bank move turned out to be: same account, same direction, same amount, at most
    /// <see cref="MatchDays"/> days apart, that side not tied to the bank yet and not taken by another move; the closest
    /// day wins. A transfer has two sides: money leaving this account ties to its ExternalID, money arriving to its
    /// ToExternalID, so one transfer written by hand matches both bank accounts.
    /// </summary>
    /// <param name="move">The bank move.</param>
    /// <param name="accountId">The account the link goes into.</param>
    /// <param name="entries">The entries around those days.</param>
    /// <param name="taken">The entry sides already matched in this sync.</param>
    /// <returns>The entry, or null.</returns>
    public static EntryRow? Match(BankMove move, Guid accountId, IEnumerable<EntryRow> entries, ISet<(Guid, bool)> taken) =>
        entries
            .Where(entry => Moves(entry, accountId) == move.Kind
                && (Arriving(entry, accountId) ? entry.ToExternalID : entry.ExternalID) is null
                && !taken.Contains((entry.EntryUniqueID, Arriving(entry, accountId)))
                && entry.Amount == move.Amount
                && Math.Abs(entry.Day.DayNumber - move.Day.DayNumber) <= MatchDays)
            .OrderBy(entry => Math.Abs(entry.Day.DayNumber - move.Day.DayNumber))
            .ThenBy(static entry => entry.CreatedAt)
            .FirstOrDefault();

    /// <summary>Whether the side of an entry that touches an account is the arriving side of a transfer.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="accountId">The account.</param>
    /// <returns>True for a transfer into the account.</returns>
    public static bool Arriving(EntryRow entry, Guid accountId) =>
        entry.Kind == (byte)EntryKind.Transfer && entry.ToAccountUniqueID == accountId && entry.AccountUniqueID != accountId;

    /// <summary>
    /// The other side of a move between two of the person's connected bank accounts: an entry another connected
    /// account brought in as money in or out, going the opposite way, with the same amount, at most
    /// <see cref="MatchDays"/> days apart. Both become one transfer, which is neither spending nor income.
    /// </summary>
    /// <param name="move">The bank move.</param>
    /// <param name="accountId">The account the link goes into.</param>
    /// <param name="others">The person's other accounts with a bank connected.</param>
    /// <param name="entries">The entries around those days.</param>
    /// <param name="taken">The entry sides already matched in this sync.</param>
    /// <returns>The entry, or null.</returns>
    public static EntryRow? Pair(BankMove move, Guid accountId, IReadOnlyCollection<Guid> others, IEnumerable<EntryRow> entries, ISet<(Guid, bool)> taken)
    {
        var opposite = move.Kind == EntryKind.Inflow ? EntryKind.Outflow : EntryKind.Inflow;
        return entries
            .Where(entry => entry.ExternalID is not null
                && entry.ToExternalID is null
                && entry.Kind == (byte)opposite
                && entry.AccountUniqueID is { } other && other != accountId && others.Contains(other)
                && !taken.Contains((entry.EntryUniqueID, false))
                && entry.Amount == move.Amount
                && Math.Abs(entry.Day.DayNumber - move.Day.DayNumber) <= MatchDays)
            .OrderBy(entry => Math.Abs(entry.Day.DayNumber - move.Day.DayNumber))
            .ThenBy(static entry => entry.CreatedAt)
            .FirstOrDefault();
    }

    /// <summary>Which way an entry moves an account: money in, money out, or not at all.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="accountId">The account.</param>
    /// <returns>Inflow, outflow, or null.</returns>
    public static EntryKind? Moves(EntryRow entry, Guid accountId) => (EntryKind)entry.Kind switch
    {
        EntryKind.Inflow when entry.AccountUniqueID == accountId => EntryKind.Inflow,
        EntryKind.Outflow when entry.AccountUniqueID == accountId => EntryKind.Outflow,
        EntryKind.Transfer when entry.AccountUniqueID == accountId => EntryKind.Outflow,
        EntryKind.Transfer when entry.ToAccountUniqueID == accountId => EntryKind.Inflow,
        _ => null,
    };

    private static DateOnly? Day(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static string Hash(EbTransaction transaction, DateOnly day, decimal amount) => Sha(string.Join('|',
        day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        amount.ToString(CultureInfo.InvariantCulture),
        transaction.TransactionAmount?.Currency,
        transaction.CreditDebitIndicator,
        transaction.Creditor?.Name,
        transaction.Debtor?.Name,
        string.Join('/', transaction.RemittanceInformation ?? [])));

    /// <summary>Takes a town off the end of the last word it is glued to ("Docelisboa" → "Doce"), more than once.</summary>
    /// <param name="text">The shop and its town.</param>
    /// <returns>The shop.</returns>
    public static string Untown(string text)
    {
        var cut = true;
        while (cut)
        {
            cut = false;
            var space = text.LastIndexOf(' ');
            var last = text[(space + 1)..];
            foreach (var town in Towns)
            {
                if (last.Length >= town.Length + 2
                    && last.EndsWith(town, StringComparison.OrdinalIgnoreCase)
                    && char.IsLetter(last[^(town.Length + 1)]))
                {
                    text = text[..(text.Length - town.Length)];
                    cut = true;
                    break;
                }
            }
        }

        return text;
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24].ToLowerInvariant();

    [GeneratedRegex(@"\b(EREF|KREF|MREF|CRED|DEBT|COAM|OAMT|SVWZ|ABWA|ABWE|IBAN|BIC|PURP)\+", RegexOptions.IgnoreCase)]
    private static partial Regex SepaTags();

    [GeneratedRegex(@"^\s*(COMPRA|COMPRAS|PAG\.?\s*COMPRA|PAGAMENTO\s+COMPRA)\s+(\d[\d\s*]{3,}\s+)?|\b\d{4}\s?\*{2,}\s?\d{0,4}\b|\bCART[ÃA]O\s+\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex CardPrefix();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^.{1,20}?\s(?:\d{7}(?:/\d+)?|[A-Za-z]{3}\s\d+\.\d+)\s(?<shop>.*[A-Za-z].*)$")]
    private static partial Regex Terminal();

    [GeneratedRegex(@"\d{4}[\s-]\d{3}\b.*$")]
    private static partial Regex PostCode();

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingDigits();
}
