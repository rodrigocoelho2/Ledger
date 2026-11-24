using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Features.Backup.Models.DTOs;
using Ledger.WebApi.Features.Backup.Services;
using Ledger.WebApi.Features.Entries.Models.DTOs;

namespace Ledger.UnitTest.Entries;

public sealed class InstalmentServiceTests
{
    private static async Task<(Harness Harness, string User, string Main)> StartAsync()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        return (harness, user, await harness.AccountIdAsync(user, "Main account"));
    }

    [Fact]
    public async Task Plan_WritesOneEntryPerMonth_AndTheBalanceWaits()
    {
        var (harness, user, main) = await StartAsync();

        // Today is 17 November 2026; the first instalment was on 2 November, five are still to come.
        var saved = await harness.EntryService.SaveAsync(new EntrySaveRequest
        {
            UserId = user, Kind = EntryKind.Outflow, Name = "Phone", Amount = 600m, Date = new DateOnly(2026, 11, 2),
            Category = "shopping", AccountId = main, Instalments = 6,
        });
        Assert.True(saved.IsSuccess);
        Assert.Equal(1, saved.Result!.PlanPart);
        Assert.Equal(6, saved.Result.PlanParts);
        Assert.Equal(100m, saved.Result.Amount);

        var december = (await harness.EntryService.MonthAsync(user, "2026-12", null, null)).Result!;
        var instalment = Assert.Single(december.Entries);
        Assert.Equal(new DateOnly(2026, 12, 2), instalment.Date);
        Assert.Equal(2, instalment.PlanPart);
        Assert.Equal(100m, december.Out);

        var accounts = (await harness.AccountService.ListAsync(user)).Result!;
        Assert.Equal(-100m, accounts.Single(static account => account.Name == "Main account").Balance);

        var overview = (await harness.OverviewService.GetAsync(user, null)).Result!;
        var plan = Assert.Single(overview.Plans);
        Assert.Equal(5, plan.Left);
        Assert.Equal(500m, plan.LeftAmount);
        Assert.Equal(new DateOnly(2026, 12, 2), plan.NextDay);
        Assert.Equal(new DateOnly(2027, 4, 1), plan.LastDay);
        Assert.Equal(500m, overview.PlansLeft);
        Assert.Equal(-100m, overview.NetWorth);
    }

    [Fact]
    public async Task Plan_IsOnlyForANewOutflow()
    {
        var (harness, user, main) = await StartAsync();

        var inflow = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Inflow, Name = "Salary", Amount = 900m, Date = harness.Today, Category = "salary", AccountId = main, Instalments = 3 });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, inflow.Status);

        var tooMany = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Car", Amount = 9000m, Date = harness.Today, Category = "vehicle", AccountId = main, Instalments = 61 });
        Assert.Contains(tooMany.Problems!.Errors, static error => error.Field == "Instalments");
    }

    [Fact]
    public async Task DeletePlan_TakesEveryInstalment_ButChangingOneKeepsItInThePlan()
    {
        var (harness, user, main) = await StartAsync();
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Sofa", Amount = 900m, Date = harness.Today, Category = "home", AccountId = main, Instalments = 3, InstalmentRule = InstalmentRule.SameDay });

        var first = (await harness.EntryService.MonthAsync(user, null, null, null)).Result!.Entries.Single();
        var changed = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Id = first.Id, Kind = EntryKind.Outflow, Name = "Sofa (shop)", Amount = 300m, Date = first.Date, Category = "home", AccountId = main });
        Assert.Equal(first.PlanId, changed.Result!.PlanId);

        Assert.Equal(3, (await harness.EntryService.DeletePlanAsync(user, first.PlanId)).Result);
        Assert.Empty(await harness.Entries.ListAsync(Guid.Parse(user)));
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await harness.EntryService.DeletePlanAsync(user, first.PlanId)).Status);
    }

    [Fact]
    public async Task VehicleSale_CountsInTheMonth_ButNotInTheSavingsRate()
    {
        var (harness, user, main) = await StartAsync();
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Inflow, Name = "Salary", Amount = 2000m, Date = harness.Today, Category = "salary", AccountId = main });
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Rent", Amount = 1500m, Date = harness.Today, Category = "home", AccountId = main });
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Inflow, Name = "Venda da mota", Amount = 4000m, Date = harness.Today, Category = "vehicle-sale", AccountId = main });

        var overview = (await harness.OverviewService.GetAsync(user, null)).Result!;
        Assert.Equal(6000m, overview.In);
        Assert.Equal(4500m, overview.Net);
        Assert.Equal(4000m, overview.OneOffIn);
        Assert.Equal(0.25m, overview.SavingsRate);

        var quick = (await harness.EntryService.QuickAsync(user, "+3500 venda carro")).Result!;
        Assert.Equal("vehicle-sale", quick.Category);
    }

    [Fact]
    public async Task Plans_SurviveTheJsonAndCsvFiles()
    {
        var (harness, user, main) = await StartAsync();
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Bike", Amount = 1200m, Date = harness.Today, Category = "gym", AccountId = main, Instalments = 10 });

        foreach (var file in new[] { (await harness.BackupService.JsonAsync(user)).Result!, (await harness.BackupService.CsvAsync(user)).Result! })
        {
            var ledger = LedgerFile.Read(file.Content)!;
            Assert.Equal(10, ledger.Entries.Count(static entry => entry.Plan is not null));
            Assert.Single(ledger.Entries.Select(static entry => entry.Plan).Distinct());

            var other = Guid.NewGuid().ToString("N");
            await harness.BackupService.ImportAsync(new ImportRequest { UserId = other, Content = file.Content });
            var overview = (await harness.OverviewService.GetAsync(other, null)).Result!;
            Assert.Equal(9, Assert.Single(overview.Plans).Left);
        }
    }
}
