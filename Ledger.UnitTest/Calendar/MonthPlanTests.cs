using Ledger.Library.Calendar;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Users.Models.Entities;

namespace Ledger.UnitTest.Calendar;

public sealed class MonthPlanTests
{
    private static readonly DateOnly December = new(2026, 12, 1);

    [Fact]
    public void Calendar_IsTheCalendarMonth()
    {
        Assert.Equal(new DateOnly(2026, 12, 1), MonthPlan.Calendar.Start(December));
        Assert.Equal(new DateOnly(2026, 12, 31), MonthPlan.Calendar.End(December));
        Assert.Equal(new DateOnly(2026, 11, 1), MonthPlan.Calendar.Of(new DateOnly(2026, 11, 26)));
    }

    [Fact]
    public void Payday25_RunsFromOneSalaryToTheNextAndTakesTheNameOfTheMonthItMostlyCovers()
    {
        var plan = new MonthPlan(25);

        // 25 Nov 2026 is a Wednesday; 25 Dec is Christmas, so January's salary comes on Thursday the 24th.
        Assert.Equal(new DateOnly(2026, 11, 25), plan.Start(December));
        Assert.Equal(new DateOnly(2026, 12, 23), plan.End(December));

        // 25 Oct 2026 is a Sunday: paid on Friday the 23rd.
        Assert.Equal(new DateOnly(2026, 10, 23), plan.Start(new DateOnly(2026, 11, 1)));
        Assert.Equal(new DateOnly(2026, 11, 24), plan.End(new DateOnly(2026, 11, 1)));
    }

    [Theory]
    [InlineData(2026, 11, 26, 12)]
    [InlineData(2026, 11, 25, 12)]
    [InlineData(2026, 11, 24, 11)]
    [InlineData(2026, 10, 23, 11)]
    [InlineData(2026, 10, 22, 10)]
    [InlineData(2026, 12, 24, 1)]
    public void Of_FindsTheMonthOfADay(int year, int month, int day, int named)
    {
        var plan = new MonthPlan(25);
        Assert.Equal(named, plan.Of(new DateOnly(year, month, day)).Month);
    }

    [Fact]
    public void SalarySeenEarly_OpensTheMonthThatDay()
    {
        // December's salary (opening January) came on the 18th, before Christmas.
        var plan = new MonthPlan(25, [new DateOnly(2026, 12, 18), new DateOnly(2026, 12, 5)]);
        Assert.Equal(new DateOnly(2026, 12, 18), plan.Start(new DateOnly(2027, 1, 1)));
        Assert.Equal(new DateOnly(2026, 12, 17), plan.End(December));
        Assert.Equal(1, plan.Of(new DateOnly(2026, 12, 20)).Month);

        // An inflow far from both paydays (10 days after one, 19 before the other) isn't a salary: December still opens on the rule.
        Assert.Equal(new DateOnly(2026, 11, 25), plan.Start(December));
    }

    [Fact]
    public void EarlyPayday_KeepsTheNameOfItsOwnMonth()
    {
        // Paid on the 2nd: 2 Nov to 1 Dec is November.
        var plan = new MonthPlan(2);
        Assert.Equal(new DateOnly(2026, 11, 2), plan.Start(new DateOnly(2026, 11, 1)));
        Assert.Equal(10, plan.Of(new DateOnly(2026, 11, 1)).Month);
    }

    [Fact]
    public void Payday31_FitsShortMonths() =>
        Assert.Equal(new DateOnly(2027, 2, 26), new MonthPlan(31).Payday(2027, 2));
}

public sealed class PaydayMonthServiceTests
{
    [Fact]
    public async Task MonthsFollowTheSalary_InEntriesAndTheOverview()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");
        harness.Store.Users.Add(new UserAccount { UserUniqueID = Guid.Parse(user), Username = "rodas" });
        await harness.Settings.SaveAsync(Guid.Parse(user), "en", false, false, 25, "FINSOLUTIA");

        // The salary came three days before the rule's Friday 23 October.
        Assert.True((await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Inflow, Name = "TRF FINSOLUTIA SA", Amount = 1500m, Date = new DateOnly(2026, 10, 20), Category = "salary", AccountId = main })).IsSuccess);
        Assert.True((await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Lunch", Amount = 9.80m, Date = new DateOnly(2026, 10, 21), Category = "groceries", AccountId = main })).IsSuccess);

        // Today is 17 November 2026: the month from 20 October to 24 November, named November.
        var month = (await harness.EntryService.MonthAsync(user, null, null, null)).Result!;
        Assert.Equal("2026-11", month.Month);
        Assert.Equal(new DateOnly(2026, 10, 20), month.From);
        Assert.Equal(new DateOnly(2026, 11, 24), month.To);
        Assert.Equal(1500m, month.In);
        Assert.Equal(9.80m, month.Out);

        Assert.Empty((await harness.EntryService.MonthAsync(user, "2026-10", null, null)).Result!.Entries);

        var overview = (await harness.OverviewService.GetAsync(user, null)).Result!;
        Assert.Equal("2026-11", overview.Month);
        Assert.True(overview.IsCurrentMonth);
        Assert.Equal(1500m, overview.In);
        Assert.Equal(new DateOnly(2026, 10, 20), overview.From);
    }
}
