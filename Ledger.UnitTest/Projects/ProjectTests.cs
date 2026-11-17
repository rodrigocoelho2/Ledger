using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Projects.Models.DTOs;

namespace Ledger.UnitTest.Projects;

public sealed class ProjectTests
{
    [Fact]
    public async Task Target_CountsWhatIsLeftOfThePriceAndOfTheWorks()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");

        var id = (await harness.ProjectService.SaveAsync(new ProjectSaveRequest { UserId = user, Name = "T1 Graça", AgreedPrice = 100000m, WorksBudget = 20000m, TargetSale = 150000m })).Result!;
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Scope = EntryScope.Flips, Name = "Deposit", Amount = 10000m, Date = harness.Today.AddDays(-20), Category = "purchase", AccountId = main, ProjectId = id });
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Scope = EntryScope.Flips, Name = "Tiles", Amount = 5000m, Date = harness.Today.AddDays(-2), Category = "works", AccountId = main, ProjectId = id });

        var project = (await harness.ProjectService.ListAsync(user)).Result!.Projects.Single();
        Assert.Equal(15000m, project.Invested);
        Assert.Equal(10000m, project.PurchasePrice);
        Assert.Equal(5000m, project.WorksSpent);

        // 15,000 in + 90,000 of the price still to pay + 15,000 of the works still to do.
        Assert.Equal(120000m, project.ProjectedCost);
        Assert.Equal(30000m, project.TargetResult);
        Assert.Equal(0.25m, project.TargetReturn);
        Assert.Null(project.Result);
    }

    [Fact]
    public async Task Save_RefusesTwoProjectsWithOneName()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        Assert.True((await harness.ProjectService.SaveAsync(new ProjectSaveRequest { UserId = user, Name = "T2 Bela Vista" })).IsSuccess);
        var twice = await harness.ProjectService.SaveAsync(new ProjectSaveRequest { UserId = user, Name = "t2 bela vista" });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, twice.Status);

        var negative = await harness.ProjectService.SaveAsync(new ProjectSaveRequest { UserId = user, Name = "Other", AgreedPrice = -1m });
        Assert.Contains(negative.Problems!.Errors, static error => error.Field == "AgreedPrice");
    }

    [Fact]
    public async Task Delete_KeepsItsEntriesWithoutAProject()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");
        var id = (await harness.ProjectService.SaveAsync(new ProjectSaveRequest { UserId = user, Name = "T0 Alfama" })).Result!;
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Scope = EntryScope.Flips, Name = "Deposit", Amount = 5000m, Date = harness.Today, Category = "purchase", AccountId = main, ProjectId = id });

        Assert.True((await harness.ProjectService.DeleteAsync(user, id)).IsSuccess);
        var list = (await harness.ProjectService.ListAsync(user)).Result!;
        Assert.Empty(list.Projects);
        Assert.Single(list.Loose);
    }
}
