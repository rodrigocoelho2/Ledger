using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Models.DTOs;

namespace Ledger.WebApi.Features.Overview.Services;

/// <summary>
/// The sums behind the overview. No database, no clock: entries in, numbers out.
/// </summary>
internal static class OverviewMath
{
    /// <summary>How many months the strip shows.</summary>
    public const int StripMonths = 6;

    /// <summary>What came in and what went out. Transfers are neither.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The inflows and the outflows.</returns>
    public static (decimal In, decimal Out) Totals(IEnumerable<EntryRow> entries)
    {
        decimal incoming = 0m, outgoing = 0m;
        foreach (var entry in entries)
        {
            if (entry.Kind == (byte)EntryKind.Inflow)
            {
                incoming += entry.Amount;
            }
            else if (entry.Kind == (byte)EntryKind.Outflow)
            {
                outgoing += entry.Amount;
            }
        }

        return (incoming, outgoing);
    }

    /// <summary>The entries of one kind grouped by category, biggest first, each with its share of that kind's total.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="kind">Inflows or outflows.</param>
    /// <returns>The slices.</returns>
    public static List<FlowPartDTO> Parts(IEnumerable<EntryRow> entries, EntryKind kind)
    {
        var groups = entries
            .Where(entry => entry.Kind == (byte)kind)
            .GroupBy(entry => Categories.Find(entry.Category)?.Key ?? Categories.Fallback(kind), StringComparer.Ordinal)
            .Select(static group => (Key: group.Key, Amount: group.Sum(static entry => entry.Amount)))
            .OrderByDescending(static group => group.Amount)
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToList();
        var total = groups.Sum(static group => group.Amount);
        return groups.Select(group => new FlowPartDTO { Key = group.Key, Amount = group.Amount, Share = Share(group.Amount, total) }).ToList();
    }

    /// <summary>A part as a share of a whole, to four decimals (0 when the whole is nothing).</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <returns>The share.</returns>
    public static decimal Share(decimal part, decimal whole) => whole == 0 ? 0m : decimal.Round(part / whole, 4, MidpointRounding.AwayFromZero);

    /// <summary>The share of what came in that was kept.</summary>
    /// <param name="incoming">What came in.</param>
    /// <param name="outgoing">What went out.</param>
    /// <returns>The rate (negative when more went out), or null when nothing came in.</returns>
    public static decimal? SavingsRate(decimal incoming, decimal outgoing) => incoming <= 0 ? null : Share(incoming - outgoing, incoming);

    /// <summary>
    /// The months of the strip, oldest first: the six that end at the month we are in, moved back when the month being
    /// looked at is older than that (it then sits third from the right), or forward when it is still to come.
    /// </summary>
    /// <param name="selected">The first day of the month being looked at.</param>
    /// <param name="current">The first day of the month we are in.</param>
    /// <returns>The first day of each month.</returns>
    public static List<DateOnly> Strip(DateOnly selected, DateOnly current)
    {
        var last = current;
        if (selected > current)
        {
            last = selected;
        }
        else if (selected < current.AddMonths(1 - StripMonths))
        {
            last = selected.AddMonths(2);
        }

        return Enumerable.Range(0, StripMonths).Select(index => last.AddMonths(index + 1 - StripMonths)).ToList();
    }

    /// <summary>What each month of a list came to, from the entries of the whole stretch.</summary>
    /// <param name="months">The first day of each month.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="current">The first day of the month we are in.</param>
    /// <returns>One line per month.</returns>
    public static List<MonthNetDTO> Nets(IEnumerable<DateOnly> months, IReadOnlyCollection<EntryRow> entries, DateOnly current) =>
        months.Select(month =>
        {
            var end = Months.End(month);
            var own = entries.Where(entry => entry.Day >= month && entry.Day <= end && entry.Kind != (byte)EntryKind.Transfer).ToList();
            var (incoming, outgoing) = Totals(own);
            return new MonthNetDTO
            {
                Month = Months.Text(month),
                Net = incoming - outgoing,
                HasEntries = own.Count > 0,
                InProgress = month == current,
                Future = month > current,
            };
        }).ToList();
}
