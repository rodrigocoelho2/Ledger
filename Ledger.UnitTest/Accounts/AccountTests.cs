using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Features.Accounts.Models.DTOs;
using Ledger.WebApi.Features.Users.Services;

namespace Ledger.UnitTest.Accounts;

public sealed class AccountTests
{
    [Fact]
    public async Task Starter_HasTheFourAccountsInOrder()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var accounts = (await harness.AccountService.ListAsync(user)).Result!;
        Assert.Equal(["Main account", "Savings", "Investments", "Meal card"], accounts.Select(static account => account.Name));
    }

    [Fact]
    public async Task Reorder_KeepsTheOrderGiven()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var accounts = (await harness.AccountService.ListAsync(user)).Result!;
        var order = accounts.Select(static account => account.Id).Reverse().ToList();

        Assert.True((await harness.AccountService.ReorderAsync(new AccountOrderRequest { UserId = user, Order = order })).IsSuccess);
        var after = (await harness.AccountService.ListAsync(user)).Result!;
        Assert.Equal(order, after.Select(static account => account.Id));
    }

    [Fact]
    public async Task Save_OpeningBalanceAndValidation()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();

        var saved = await harness.AccountService.SaveAsync(new AccountSaveRequest { UserId = user, Name = "Holiday pot", Kind = AccountKind.Savings, OpeningBalance = 250m });
        Assert.True(saved.IsSuccess);
        Assert.Equal(250m, saved.Result!.Balance);

        var unnamed = await harness.AccountService.SaveAsync(new AccountSaveRequest { UserId = user, Name = " ", Kind = AccountKind.Savings });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, unnamed.Status);
    }

    [Fact]
    public void Passwords_HashAndVerify()
    {
        var stored = PasswordHasher.Hash("correct horse battery");
        Assert.True(PasswordHasher.Verify("correct horse battery", stored));
        Assert.False(PasswordHasher.Verify("Correct horse battery", stored));
        Assert.False(PasswordHasher.Verify("anything", "not-a-hash"));
        Assert.NotEqual(stored, PasswordHasher.Hash("correct horse battery"));
    }
}
