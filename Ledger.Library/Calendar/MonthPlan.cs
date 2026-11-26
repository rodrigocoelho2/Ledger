namespace Ledger.Library.Calendar;

/// <summary>
/// Where a person's months begin. Without a payday a month is the calendar month. With one, it runs from a salary to
/// the day before the next: the payday is a day of the month, moved back to the business day before when it falls on
/// a weekend or a holiday, and a salary the bank brought in near that day wins over the rule (paid early for
/// Christmas, say), so the month opens the day the money actually came in. A month is named after the calendar month
/// holding most of its days: with a payday after the 15th, the salary of 25 November opens "December".
/// </summary>
public sealed class MonthPlan
{
    /// <summary>How many days before the payday a salary still opens the month.</summary>
    public const int EarlyDays = 12;

    /// <summary>How many days after the payday a salary still opens the month.</summary>
    public const int LateDays = 6;

    private readonly DateOnly[] _salaries;

    /// <summary>Initializes a new instance of the <see cref="MonthPlan"/> class.</summary>
    /// <param name="payDay">The day of the month the salary is due (2 to 31; anything else: calendar months).</param>
    /// <param name="salaries">The days salaries came in (any order; empty: the rule alone).</param>
    public MonthPlan(int payDay, IEnumerable<DateOnly>? salaries = null)
    {
        PayDay = payDay is >= 2 and <= 31 ? payDay : 0;
        _salaries = PayDay == 0 || salaries is null ? [] : [.. salaries.Distinct().Order()];
    }

    /// <summary>Gets calendar months.</summary>
    public static MonthPlan Calendar { get; } = new(0);

    /// <summary>Gets the day of the month the salary is due (0: calendar months).</summary>
    public int PayDay { get; }

    /// <summary>Gets a value indicating whether months are calendar months.</summary>
    public bool IsCalendar => PayDay == 0;

    /// <summary>Gets how many months the name runs ahead of the payday: 1 when most of the month is in the next one.</summary>
    private int Ahead => PayDay > 15 ? 1 : 0;

    /// <summary>The day the rule puts the salary of a calendar month on.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1 to 12).</param>
    /// <returns>The payday, or the business day before it.</returns>
    public DateOnly Payday(int year, int month) =>
        BusinessDays.OnOrBefore(new DateOnly(year, month, Math.Min(Math.Max(PayDay, 1), DateTime.DaysInMonth(year, month))));

    /// <summary>The first day of a month.</summary>
    /// <param name="month">The month, as named (any day of it: 1 Dec 2026 is "December").</param>
    /// <returns>The day.</returns>
    public DateOnly Start(DateOnly month)
    {
        var named = new DateOnly(month.Year, month.Month, 1);
        if (IsCalendar)
        {
            return named;
        }

        var paid = named.AddMonths(-Ahead);
        var rule = Payday(paid.Year, paid.Month);
        DateOnly? best = null;
        foreach (var salary in _salaries)
        {
            if (salary < rule.AddDays(-EarlyDays))
            {
                continue;
            }

            if (salary > rule.AddDays(LateDays))
            {
                break;
            }

            if (best is not { } kept || Math.Abs(salary.DayNumber - rule.DayNumber) < Math.Abs(kept.DayNumber - rule.DayNumber))
            {
                best = salary;
            }
        }

        return best ?? rule;
    }

    /// <summary>The last day of a month.</summary>
    /// <param name="month">The month, as named (any day of it).</param>
    /// <returns>The day before the next month starts.</returns>
    public DateOnly End(DateOnly month) => Start(new DateOnly(month.Year, month.Month, 1).AddMonths(1)).AddDays(-1);

    /// <summary>The month a day belongs to.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The month, as named (its first calendar day).</returns>
    public DateOnly Of(DateOnly day)
    {
        var month = new DateOnly(day.Year, day.Month, 1);
        if (IsCalendar)
        {
            return month;
        }

        if (day >= Start(month.AddMonths(1)))
        {
            return month.AddMonths(1);
        }

        return day < Start(month) ? month.AddMonths(-1) : month;
    }
}
