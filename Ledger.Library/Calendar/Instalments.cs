using Ledger.Library.Enums;

namespace Ledger.Library.Calendar;

/// <summary>
/// A purchase paid in equal monthly instalments, without interest: the days and the amounts.
/// </summary>
public static class Instalments
{
    /// <summary>The fewest instalments a plan can have.</summary>
    public const int Fewest = 2;

    /// <summary>The most instalments a plan can have (five years).</summary>
    public const int Most = 60;

    /// <summary>
    /// The instalments of a plan. The total is split in equal parts to the cent; the cents that don't divide go to the
    /// first instalment, so the parts always add up to the total.
    /// </summary>
    /// <param name="total">What the purchase costs.</param>
    /// <param name="count">How many instalments.</param>
    /// <param name="first">The day of the first instalment.</param>
    /// <param name="rule">When the others are paid.</param>
    /// <returns>Each instalment's day and amount, in order.</returns>
    public static List<(DateOnly Day, decimal Amount)> Schedule(decimal total, int count, DateOnly first, InstalmentRule rule)
    {
        if (count < 1)
        {
            return [];
        }

        var cents = (long)decimal.Round(total * 100m, 0, MidpointRounding.AwayFromZero);
        var part = cents / count;
        var rest = cents - (part * count);

        var plan = new List<(DateOnly, decimal)>(count);
        for (var index = 0; index < count; index++)
        {
            var amount = (part + (index == 0 ? rest : 0)) / 100m;
            plan.Add((Day(first, index, rule), amount));
        }

        return plan;
    }

    /// <summary>The day of an instalment.</summary>
    /// <param name="first">The day of the first instalment.</param>
    /// <param name="index">Which instalment, from 0.</param>
    /// <param name="rule">When the instalments after the first are paid.</param>
    /// <returns>The day.</returns>
    public static DateOnly Day(DateOnly first, int index, InstalmentRule rule)
    {
        if (index == 0)
        {
            return first;
        }

        var month = new DateOnly(first.Year, first.Month, 1).AddMonths(index);
        return rule == InstalmentRule.SameDay
            ? new DateOnly(month.Year, month.Month, Math.Min(first.Day, DateTime.DaysInMonth(month.Year, month.Month)))
            : BusinessDays.FirstOfMonth(month);
    }

    /// <summary>The day the first instalment usually falls on: the first business day of next month.</summary>
    /// <param name="today">Today.</param>
    /// <returns>The day.</returns>
    public static DateOnly DefaultFirst(DateOnly today) => BusinessDays.FirstOfMonth(today.AddMonths(1));
}
