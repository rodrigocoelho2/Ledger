using Ledger.Library.Calendar;
using Ledger.Library.Enums;

namespace Ledger.UnitTest.Calendar;

public sealed class BusinessDayTests
{
    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2025, 4, 20)]
    [InlineData(2024, 3, 31)]
    public void Easter_IsRight(int year, int month, int day) => Assert.Equal(new DateOnly(year, month, day), BusinessDays.Easter(year));

    [Theory]
    [InlineData(2026, 11, 2)]  // 1 Nov is a Sunday and All Saints
    [InlineData(2026, 12, 2)]  // 1 Dec is Restoration of Independence
    [InlineData(2027, 1, 4)]   // 1 Jan holiday, then the weekend
    [InlineData(2026, 5, 4)]   // 1 May is a Friday and Labour Day
    [InlineData(2026, 6, 1)]   // an ordinary Monday
    [InlineData(2026, 8, 3)]   // 1 Aug is a Saturday
    [InlineData(2029, 4, 2)]   // 30 Mar is Good Friday; 1 Apr is Easter Sunday
    public void FirstOfMonth_SkipsWeekendsAndHolidays(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), BusinessDays.FirstOfMonth(year, month));

    [Fact]
    public void CorpusChristi_IsAHoliday() => Assert.True(BusinessDays.IsHoliday(new DateOnly(2026, 6, 4)));
}

public sealed class InstalmentTests
{
    [Fact]
    public void Schedule_SplitsToTheCentAndAddsUp()
    {
        var plan = Instalments.Schedule(1000m, 3, new DateOnly(2026, 12, 2), InstalmentRule.FirstBusinessDay);
        Assert.Equal([333.34m, 333.33m, 333.33m], plan.Select(static part => part.Amount));
        Assert.Equal(1000m, plan.Sum(static part => part.Amount));
        Assert.Equal([new DateOnly(2026, 12, 2), new DateOnly(2027, 1, 4), new DateOnly(2027, 2, 1)], plan.Select(static part => part.Day));
    }

    [Fact]
    public void Schedule_SameDay_KeepsTheDayAndShortensForShortMonths()
    {
        var plan = Instalments.Schedule(600m, 4, new DateOnly(2027, 1, 31), InstalmentRule.SameDay);
        Assert.Equal([new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30)], plan.Select(static part => part.Day));
        Assert.All(plan, static part => Assert.Equal(150m, part.Amount));
    }

    [Fact]
    public void DefaultFirst_IsNextMonthsFirstBusinessDay() =>
        Assert.Equal(new DateOnly(2026, 12, 2), Instalments.DefaultFirst(new DateOnly(2026, 11, 24)));
}
