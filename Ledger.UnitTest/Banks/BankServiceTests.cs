using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Features.Accounts.Models.DTOs;
using Ledger.WebApi.Features.Banks.Models.DTOs;
using Ledger.WebApi.Features.Banks.Services;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Entries.Services;

namespace Ledger.UnitTest.Banks;

public sealed class BankServiceTests
{
    private static async Task<(Harness Harness, string User, string Main, string Link)> ConnectedAsync()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var main = await harness.AccountIdAsync(user, "Main account");

        var start = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user, RedirectUrl = "https://ledger.example/bank/callback" })).Result!;
        var status = (await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State })).Result!;
        return (harness, user, main, Assert.Single(status.Links).Id);
    }

    [Fact]
    public async Task Start_UsesLedgersCallbackWhenRegistered_ElseAsksToPaste()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();

        var own = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user, RedirectUrl = "https://ledger.example/bank/callback/" })).Result!;
        Assert.False(own.Paste);
        Assert.Equal("https://ledger.example/bank/callback", own.RedirectUrl);
        Assert.Equal(own.State, harness.Bank.LastState);

        var elsewhere = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user, RedirectUrl = "http://192.168.1.10:5100/bank/callback" })).Result!;
        Assert.True(elsewhere.Paste);
        Assert.Equal("https://ledger.example/bank/callback", elsewhere.RedirectUrl);

        var unknown = await harness.BankService.StartAsync(new BankStartRequest { UserId = user, Bank = "Banco Que Não Existe" });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, unknown.Status);
    }

    [Fact]
    public async Task Complete_NeedsTheStateOfThisPerson()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var someoneElse = await harness.StarterAsync();
        var start = (await harness.BankService.StartAsync(new BankStartRequest { UserId = someoneElse })).Result!;

        var stolen = await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, stolen.Status);

        var forged = await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State.Replace(someoneElse, user, StringComparison.Ordinal) });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, forged.Status);

        // The person pastes the whole address they landed on.
        var pasted = await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = someoneElse, Code = $"https://ledger.example/bank/callback?state={Uri.EscapeDataString(start.State)}&code=good" });
        Assert.True(pasted.IsSuccess);
        var link = Assert.Single(pasted.Result!.Links);
        Assert.Equal("Crédito Agrícola", link.BankName);
        Assert.Equal("1234", link.IbanTail);
        Assert.Null(link.AccountId);
    }

    [Fact]
    public void ReadCallback_UnderstandsCodesAddressesAndErrors()
    {
        Assert.Equal(("abc", "s1", (string?)null), BankService.ReadCallback("abc", "s1"));
        Assert.Equal(("abc", "s 2", (string?)null), BankService.ReadCallback("https://x.example/cb?code=abc&state=s%202#top", null));
        Assert.Equal(((string?)null, (string?)null, "User cancelled"), BankService.ReadCallback("https://x.example/cb?error=access_denied&error_description=User+cancelled", null));
    }

    [Fact]
    public async Task Assign_BringsTheMonth_MatchesWhatWasWritten_AndAgreesWithTheBank()
    {
        var (harness, user, main, link) = await ConnectedAsync();

        // Written by hand on the 10th; the bank books it on the 11th.
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Outflow, Name = "Gasolina", Amount = 45m, Date = new DateOnly(2026, 11, 10), Category = "transport", AccountId = main });
        harness.Bank.Transactions.AddRange([
            FakeBank.Move("old", -99m, new DateOnly(2026, 10, 30), "Before the month"),
            FakeBank.Move("t1", 1500m, new DateOnly(2026, 11, 3), "EMPRESA LDA", "ORDENADO NOVEMBRO"),
            FakeBank.Move("t2", -9.8m, new DateOnly(2026, 11, 5), null, "COMPRA 4512 RESTAURANTE O PATEO"),
            FakeBank.Move("t3", -45m, new DateOnly(2026, 11, 11), "GALP"),
        ]);
        harness.Bank.Balance = 2000m;

        var sync = await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = link, AccountId = main, ImportFrom = new DateOnly(2026, 11, 1) });
        Assert.True(sync.IsSuccess);
        Assert.Equal(2, sync.Result!.Imported);
        Assert.Equal(1, sync.Result.Matched);
        Assert.Equal((new DateOnly(2026, 11, 1), harness.Today), harness.Bank.Asked[0]);

        var month = (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!;
        Assert.Equal(3, month.Entries.Count);
        Assert.Equal(2, month.ReviewCount);
        var lunch = month.Entries.Single(static entry => entry.Amount == 9.8m);
        Assert.Equal("Restaurante O Pateo", lunch.Name);
        Assert.Equal("groceries", lunch.Category);
        Assert.True(lunch.Review);
        Assert.True(lunch.FromBank);
        var fuel = month.Entries.Single(static entry => entry.Name == "Gasolina");
        Assert.True(fuel.FromBank);
        Assert.False(fuel.Review);
        Assert.Equal("salary", month.Entries.Single(static entry => entry.Amount == 1500m).Category);

        // The opening balance takes up what came before, so Ledger shows what the bank shows.
        var account = (await harness.AccountService.ListAsync(user)).Result!.Single(static row => row.Name == "Main account");
        Assert.Equal(2000m, account.Balance);
        Assert.Equal("Crédito Agrícola", account.BankName);
        Assert.Equal(0m, account.BankDifference);

        var review = (await harness.EntryService.MonthAsync(user, "2026-11", null, EntryService.ReviewFilter)).Result!;
        Assert.Equal(2, review.Entries.Count);
        var reviewed = await harness.EntryService.ReviewedAsync(new EntryReviewedRequest { UserId = user, Ids = [lunch.Id] });
        Assert.Equal(1, reviewed.Result);
        Assert.Equal(1, (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.ReviewCount);
    }

    [Fact]
    public async Task Sync_NeverBringsTheSameMoveTwice_NorOneThePersonDeleted()
    {
        var (harness, user, main, link) = await ConnectedAsync();
        harness.Bank.Transactions.AddRange([FakeBank.Move("t1", -20m, new DateOnly(2026, 11, 12), "LIDL"), FakeBank.Move("t2", -30m, new DateOnly(2026, 11, 13), "Worten")]);
        await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = link, AccountId = main, ImportFrom = new DateOnly(2026, 11, 1) });

        var again = (await harness.BankService.SyncAsync(user, link)).Result!;
        Assert.Equal(0, again.Imported);
        Assert.Equal(0, again.Matched);
        Assert.Equal((harness.Today.AddDays(-BankService.LookBackDays), harness.Today), harness.Bank.Asked[^1]);

        var worten = (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries.Single(static entry => entry.Amount == 30m);
        await harness.EntryService.DeleteAsync(user, worten.Id);
        Assert.Equal(0, (await harness.BankService.SyncAsync(user, link)).Result!.Imported);
        Assert.Single((await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries);
    }

    [Fact]
    public async Task ConnectingAgain_DoesNotBringWhatIsAlreadyThere()
    {
        var (harness, user, main, link) = await ConnectedAsync();
        harness.Bank.Transactions.AddRange([FakeBank.Move("t1", -20m, new DateOnly(2026, 11, 12), "LIDL"), FakeBank.Move("t2", 1500m, new DateOnly(2026, 11, 3), "EMPRESA LDA")]);
        Assert.Equal(2, (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = link, AccountId = main, ImportFrom = new DateOnly(2026, 11, 1) })).Result!.Imported);

        // Disconnected and connected again to bring older months: a new link, the same moves.
        Assert.True((await harness.BankService.DeleteAsync(user, link)).IsSuccess);
        var start = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user, RedirectUrl = "https://ledger.example/bank/callback" })).Result!;
        var again = Assert.Single((await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State })).Result!.Links).Id;
        Assert.NotEqual(link, again);
        harness.Bank.Transactions.Add(FakeBank.Move("t0", -5m, new DateOnly(2026, 10, 20), "CAFE"));

        var sync = (await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = again, AccountId = main, ImportFrom = new DateOnly(2026, 10, 1) })).Result!;
        Assert.Equal(1, sync.Imported);
        Assert.Equal(2, (await harness.EntryService.MonthAsync(user, "2026-11", null, null)).Result!.Entries.Count);
    }

    [Fact]
    public async Task Sync_KeepsTheError_AndAnEndedConsentSaysExpired()
    {
        var (harness, user, main, link) = await ConnectedAsync();
        harness.Bank.Error = "500 bank down";
        var failed = await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = link, AccountId = main });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, failed.Status);
        var status = (await harness.BankService.StatusAsync(user)).Result!;
        Assert.Equal("500 bank down", Assert.Single(status.Links).LastError);

        // The worker doesn't ask a failing bank again straight away.
        Assert.Equal(0, await harness.BankService.SyncDueAsync(CancellationToken.None));

        harness.Bank.Session.Access!.ValidUntil = harness.Clock.UtcNow.AddMinutes(-1);
        var renewed = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user })).Result!;
        await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = renewed.State });
        var expired = Assert.Single((await harness.BankService.StatusAsync(user)).Result!.Links);
        Assert.True(expired.Expired);
        Assert.Equal(BankService.Expired, expired.LastError);
        Assert.Equal(main, expired.AccountId);
    }

    [Fact]
    public async Task Assign_OneBankAccountPerLedgerAccount_AndNewMakesOne()
    {
        var (harness, user, main, link) = await ConnectedAsync();
        harness.Bank.Session = new()
        {
            SessionId = "session-2",
            Aspsp = harness.Bank.Session.Aspsp,
            Access = harness.Bank.Session.Access,
            Accounts = [new() { Uid = "acc-2", Name = "Conta poupança", AccountId = new() { Iban = "PT50000000000000000009876" } }],
        };
        var start = (await harness.BankService.StartAsync(new BankStartRequest { UserId = user })).Result!;
        var links = (await harness.BankService.CompleteAsync(new BankCompleteRequest { UserId = user, Code = "good", State = start.State })).Result!.Links;
        Assert.Equal(2, links.Count);
        var second = links.Single(static row => row.IbanTail == "9876").Id;

        Assert.True((await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = link, AccountId = main })).IsSuccess);
        var taken = await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = second, AccountId = main });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, taken.Status);

        var created = await harness.BankService.AssignAsync(new BankAssignRequest { UserId = user, LinkId = second, AccountId = BankAssignRequest.NewAccount });
        Assert.Equal("Conta poupança", created.Result!.Link!.AccountName);

        Assert.True((await harness.BankService.DeleteAsync(user, second)).IsSuccess);
        Assert.Contains("session-2", harness.Bank.Ended);
        Assert.Contains((await harness.AccountService.ListAsync(user)).Result!, static account => account.Name == "Conta poupança");
    }

    [Fact]
    public async Task Obol_ValuesTheAccount_AndTheMonthChangeFollowsTheValues()
    {
        var harness = new Harness();
        var user = await harness.StarterAsync();
        var investments = await harness.AccountIdAsync(user, "Investments");
        harness.Obol.Values["rodas"] = 12_345.67m;

        var saved = await harness.AccountService.SaveAsync(new AccountSaveRequest { UserId = user, Id = investments, Name = "Investments", Kind = AccountKind.Investments, ValueSource = ValueSource.Obol, ValueRef = "rodas" });
        Assert.True(saved.IsSuccess);
        Assert.Equal(12_345.67m, saved.Result!.Balance);
        Assert.Equal(harness.Today, saved.Result.ValueDate);

        // A transfer into it doesn't move the value: Obol already counts the money.
        var main = await harness.AccountIdAsync(user, "Main account");
        await harness.EntryService.SaveAsync(new EntrySaveRequest { UserId = user, Kind = EntryKind.Transfer, Name = "To the broker", Amount = 500m, Date = harness.Today, AccountId = main, ToAccountId = investments });
        Assert.Equal(12_345.67m, (await harness.AccountService.ListAsync(user)).Result!.Single(static account => account.Name == "Investments").Balance);

        await harness.Accounts.SaveValueAsync(Guid.Parse(user), Guid.Parse(investments), new DateOnly(2026, 10, 31), 12_000m);
        var overview = (await harness.OverviewService.GetAsync(user, null)).Result!;
        Assert.Equal(345.67m, overview.Accounts.Single(static account => account.Name == "Investments").MonthChange);
        Assert.Equal(12_345.67m - 500m, overview.NetWorth);

        harness.Obol.Error = "timeout";
        var failed = await harness.AccountService.RefreshAsync(user, investments);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, failed.Status);
        Assert.Equal("timeout", (await harness.AccountService.ListAsync(user)).Result!.Single(static account => account.Name == "Investments").ValueError);

        harness.Obol.Error = null;
        harness.Obol.Values["rodas"] = 12_400m;
        Assert.Equal(1, await harness.Valuation.RefreshAllAsync(CancellationToken.None));
        Assert.Equal(12_400m, (await harness.AccountService.ListAsync(user)).Result!.Single(static account => account.Name == "Investments").Balance);

        // Back to its entries: the balance is worked out again.
        var back = await harness.AccountService.SaveAsync(new AccountSaveRequest { UserId = user, Id = investments, Name = "Investments", Kind = AccountKind.Investments });
        Assert.Null(back.Result!.ValueSource);
        Assert.Equal(500m, back.Result.Balance);
    }
}
