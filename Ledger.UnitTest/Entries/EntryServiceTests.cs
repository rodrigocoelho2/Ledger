using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Features.Entries.Models.DTOs;

namespace Ledger.UnitTest.Entries;

public sealed class EntryServiceTests
{
    [Fact]
    public async Task Save_ThenTheMonthListsIt()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");

        var saved = await harness.EntryService.SaveAsync(new EntrySaveRequest
        {
            UserId = user,
            Kind = EntryKind.Outflow,
            Name = "Lunch",
            Amount = 9.80m,
            Date = new DateOnly(2026, 11, 3),
            Category = "groceries",
            AccountId = main,
        });
        Assert.True(saved.IsSuccess);

        var month = (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!;
        var entry = Assert.Single(month.Entries);
        Assert.Equal("Lunch", entry.Name);
        Assert.Equal("Main account", entry.AccountName);
        Assert.Equal(9.80m, month.Out);
        Assert.Equal(["groceries"], month.Categories);

        var accounts = (await harness.AccountService.ListAsync(user)).Result!;
        Assert.Equal(-9.80m, accounts.Single(static account => account.Name == "Main account").Balance);
    }

    [Fact]
    public async Task Save_RefusesNonsense()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");

        var noAmount = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "x", Amount = 0m, Date = harness.Today, Category = "other", AccountId = main });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, noAmount.Status);
        Assert.Contains(noAmount.Problems!.Errors, static error => error.Field == "Amount");

        var sameAccount = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Transfer, Name = "x", Amount = 10m, Date = harness.Today, AccountId = main, ToAccountId = main });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, sameAccount.Status);

        var wrongCategory = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "x", Amount = 10m, Date = harness.Today, Category = "salary", AccountId = main });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, wrongCategory.Status);
    }

    [Fact]
    public async Task Transfer_MovesMoneyWithoutCountingAsSpending()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");
        var savings = await harness.AccountIdAsync(user, "Savings");

        var saved = await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Transfer, Name = "To savings", Amount = 300m, Date = harness.Today, AccountId = main, ToAccountId = savings });
        Assert.True(saved.IsSuccess);

        var overview = (await harness.OverviewService.GetAsync(user, null)).Result!;
        Assert.Equal(0m, overview.Out);
        Assert.Equal(0m, overview.NetWorth);
        Assert.Equal(-300m, overview.Accounts.Single(static account => account.Name == "Main account").Balance);
        Assert.Equal(300m, overview.Accounts.Single(static account => account.Name == "Savings").Balance);
    }

    [Fact]
    public async Task Quick_GuessesNewWordsAndRemembersOldOnes()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");

        var lunch = (await harness.EntryService.QuickAsync(user, "12,40 almoço")).Result!;
        Assert.True(lunch.Understood);
        Assert.Equal(12.40m, lunch.Amount);
        Assert.Equal("groceries", lunch.Category);
        Assert.Equal("Meal card", lunch.AccountName);
        Assert.False(lunch.Remembered);

        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Padel with Rui", Amount = 8m, Date = harness.Today, Category = "gym", AccountId = main });
        var again = (await harness.EntryService.QuickAsync(user, "8 padel with rui")).Result!;
        Assert.True(again.Remembered);
        Assert.Equal("gym", again.Category);
        Assert.Equal("Main account", again.AccountName);

        var salary = (await harness.EntryService.QuickAsync(user, "+1850 salário")).Result!;
        Assert.Equal(EntryKind.Inflow, salary.Kind);
        Assert.Equal("salary", salary.Category);

        var nothing = await harness.EntryService.QuickAsync(user, "lunch");
        Assert.False(nothing.IsSuccess);
    }

    [Fact]
    public async Task Delete_TakesItOut()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");
        var saved = (await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Gym", Amount = 45m, Date = harness.Today, Category = "gym", AccountId = main })).Result!;

        Assert.True((await harness.EntryService.DeleteAsync(user, saved.Id)).IsSuccess);
        Assert.Empty((await harness.EntryService.MonthAsync(user, null, null, null)).Result!.Entries);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await harness.EntryService.DeleteAsync(user, saved.Id)).Status);
    }
}
