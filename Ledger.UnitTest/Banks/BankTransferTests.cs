using Ledger.Library.Enums;
using Ledger.WebApi.Features.Banks.Http;
using Ledger.WebApi.Features.Banks.Models.DTOs;
using Ledger.WebApi.Features.Entries.Models.DTOs;

namespace Ledger.UnitTest.Banks;

/// <summary>Two accounts at the same bank (main and savings), and the money moved between them.</summary>
public sealed class BankTransferTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    private static async Task<(Harness Harness, string User, string Main, string Savings, string MainLink, string SavingsLink)> TwoAccountsAsync()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");
        var savings = await harness.AccountIdAsync(user, "Savings");
        harness.Bank.Session.Accounts =
        [
            new EbAccount { Uid = "principal", Name = "Principal", AccountId = new EbAccountId { Iban = "PT50000000000000000001111" } },
            new EbAccount { Uid = "poupanca", Name = "Poupança", AccountId = new EbAccountId { Iban = "PT50000000000000000002222" } },
        ];
        harness.Bank.ByAccount["principal"] = [];
        harness.Bank.ByAccount["poupanca"] = [];

        var start = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user })).Result!;
        var links = (await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State })).Result!.Links;
        Assert.Equal(2, links.Count);
        return (harness, user, main, savings, links.Single(static link => link.IbanTail == "1111").Id, links.Single(static link => link.IbanTail == "2222").Id);
    }

    [Fact]
    public async Task MovingMoneyToSavings_BecomesOneTransfer_NotSpendingAndIncome()
    {
        var (harness, user, main, savings, mainLink, savingsLink) = await TwoAccountsAsync();
        harness.Bank.ByAccount["principal"].AddRange([FakeBank.Move("p1", -300m, Day, "Rodrigo Coelho", "TRF P/ POUPANCA"), FakeBank.Move("p2", -12.5m, Day, "Lidl")]);
        harness.Bank.ByAccount["poupanca"].Add(FakeBank.Move("s1", 300m, Day.AddDays(1), "Rodrigo Coelho", "TRF DA ORDEM"));
        harness.Bank.BalanceOf["principal"] = 700m;
        harness.Bank.BalanceOf["poupanca"] = 1300m;

        Assert.Equal(2, (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = mainLink, AccountId = main, ImportFrom = new DateOnly(2026, 11, 1) })).Result!.Imported);
        var second = (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = savingsLink, AccountId = savings, ImportFrom = new DateOnly(2026, 11, 1) })).Result!;
        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.Transfers);

        var month = (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!;
        Assert.Equal(2, month.Entries.Count);
        var transfer = month.Entries.Single(static entry => entry.Kind == EntryKind.Transfer);
        Assert.Equal(main, transfer.AccountId);
        Assert.Equal(savings, transfer.ToAccountId);
        Assert.Equal(300m, transfer.Amount);
        Assert.Equal(12.5m, month.Out);
        Assert.Equal(0m, month.In);

        var accounts = (await harness.AccountService.ListAsync(user)).Result!;
        Assert.Equal(700m, accounts.Single(static account => account.Name == "Main account").Balance);
        Assert.Equal(1300m, accounts.Single(static account => account.Name == "Savings").Balance);

        // Again, both ways round: nothing new.
        Assert.Equal(0, (await harness.BankService.SyncAsync(user, mainLink)).Result!.Imported);
        var again = (await harness.BankService.SyncAsync(user, savingsLink)).Result!;
        Assert.Equal(0, again.Imported + again.Transfers + again.Matched);
        Assert.Equal(2, (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries.Count);

        // Deleting the transfer keeps both sides out.
        await harness.EntryService.DeleteAsync(user, transfer.Id);
        await harness.BankService.SyncAsync(user, mainLink);
        await harness.BankService.SyncAsync(user, savingsLink);
        Assert.Single((await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries);
    }

    [Fact]
    public async Task ATransferWrittenByHand_IsConfirmedByBothAccounts()
    {
        var (harness, user, main, savings, mainLink, savingsLink) = await TwoAccountsAsync();
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Transfer, Name = "Para a poupança", Amount = 200m, Date = Day, AccountId = main, ToAccountId = savings });
        harness.Bank.ByAccount["principal"].Add(FakeBank.Move("p1", -200m, Day, "Rodrigo Coelho"));
        harness.Bank.ByAccount["poupanca"].Add(FakeBank.Move("s1", 200m, Day.AddDays(1), "Rodrigo Coelho"));

        var first = (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = mainLink, AccountId = main, ImportFrom = new DateOnly(2026, 11, 1) })).Result!;
        var second = (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = savingsLink, AccountId = savings, ImportFrom = new DateOnly(2026, 11, 1) })).Result!;
        Assert.Equal((0, 1), (first.Imported, first.Matched));
        Assert.Equal((0, 1), (second.Imported, second.Matched));

        var transfer = Assert.Single((await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries);
        Assert.Equal("Para a poupança", transfer.Name);
        Assert.True(transfer.FromBank);
        Assert.False(transfer.Review);
    }

    [Fact]
    public async Task MoneyFromSomeoneElse_StaysIncome()
    {
        var (harness, user, main, savings, mainLink, savingsLink) = await TwoAccountsAsync();

        // Only the savings account is connected to a bank: a €300 payment out of main isn't the other half of anything.
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Renda", Amount = 300m, Date = Day, Category = "home", AccountId = main });
        harness.Bank.ByAccount["poupanca"].Add(FakeBank.Move("s1", 300m, Day, "Joana Silva"));
        var result = (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = savingsLink, AccountId = savings, ImportFrom = new DateOnly(2026, 11, 1) })).Result!;
        Assert.Equal((1, 0), (result.Imported, result.Transfers));
        Assert.Equal(300m, (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.In);
    }
}
