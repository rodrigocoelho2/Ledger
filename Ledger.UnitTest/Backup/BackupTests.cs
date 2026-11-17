using Ledger.Library.Enums;
using Ledger.WebApi.Features.Backup.Models.DTOs;
using Ledger.WebApi.Features.Backup.Services;

namespace Ledger.UnitTest.Backup;

public sealed class BackupTests
{
    [Fact]
    public async Task Json_RoundTripsTheWholeLedger()
    {
        var harness = new Harness();
        var demo = await harness.DemoAsync();
        var file = (await harness.BackupService.JsonAsync(demo)).Result!;
        Assert.EndsWith(".json", file.FileName, StringComparison.Ordinal);

        var empty = Guid.NewGuid().ToString("N");
        var result = (await harness.BackupService.ImportAsync(new ImportRequest { UserId = empty, Content = file.Content })).Result!;
        Assert.Equal(0, result.Skipped);
        Assert.Equal(5, result.AccountsCreated);
        Assert.Equal(2, result.ProjectsCreated);

        var before = (await harness.OverviewService.GetAsync(demo, "2026-10")).Result!;
        var after = (await harness.OverviewService.GetAsync(empty, "2026-10")).Result!;
        Assert.Equal(before.In, after.In);
        Assert.Equal(before.Out, after.Out);
        Assert.Equal(before.NetWorth, after.NetWorth);
        Assert.Equal(before.Cap, after.Cap);

        var open = (await harness.ProjectService.ListAsync(empty)).Result!.Projects.Single(static project => project.State == ProjectState.InProgress);
        Assert.Equal(75000m, open.AgreedPrice);
    }

    [Fact]
    public async Task Import_TheSameFileTwiceAddsNothingTheSecondTime()
    {
        var harness = new Harness();
        var demo = await harness.DemoAsync();
        var csv = (await harness.BackupService.CsvAsync(demo)).Result!;

        var other = Guid.NewGuid().ToString("N");
        var first = (await harness.BackupService.ImportAsync(new ImportRequest { UserId = other, Content = csv.Content })).Result!;
        var second = (await harness.BackupService.ImportAsync(new ImportRequest { UserId = other, Content = csv.Content })).Result!;
        Assert.True(first.Added > 100);
        Assert.Equal(0, second.Added);
        Assert.Equal(first.Added, second.AlreadyThere);

        var rental = (await harness.RentalService.GetAsync(other, "2026-10")).Result!;
        Assert.Equal(1665.00m, rental.Revenue);
        Assert.Equal(19, rental.NightsBooked);
    }

    [Fact]
    public async Task Csv_InPortuguese_ReadsBack()
    {
        var harness = new Harness("pt");
        var demo = await harness.DemoAsync();
        var csv = (await harness.BackupService.CsvAsync(demo)).Result!;
        Assert.Contains("Alimentação", csv.Content, StringComparison.Ordinal);

        var ledger = LedgerFile.Read(csv.Content)!;
        Assert.Equal(0, ledger.Skipped);
        Assert.Contains(ledger.Entries, static entry => entry.Category == "groceries");
        Assert.Contains(ledger.Entries, static entry => entry.Scope == EntryScope.Rental && entry.Nights > 0);
    }

    [Fact]
    public void Read_TheOldSingleFileLedger()
    {
        const string old = """
            {
              "accounts": [
                { "id": "a1", "name": "Conta principal", "kind": "corrente", "start": 1200 },
                { "id": "a2", "name": "Cartão refeição", "kind": "refeicao", "start": 0 }
              ],
              "caps": { "2026-05": 1300 },
              "tx": [
                { "k": "in", "name": "Salário", "amount": 1850, "date": "2026-05-25", "catId": "salario", "accId": "a1", "scope": "pessoal" },
                { "k": "out", "name": "Almoço", "amount": 9.8, "date": "2026-05-26", "catId": "alimentacao", "accId": "a2" },
                { "k": "out", "name": "", "amount": 3, "date": "2026-05-26" }
              ]
            }
            """;

        var ledger = LedgerFile.Read(old)!;
        Assert.Equal(2, ledger.Accounts.Count);
        Assert.Equal(AccountKind.MealCard, ledger.Accounts[1].Kind);
        Assert.Equal(1300m, ledger.Caps[new DateOnly(2026, 5, 1)]);
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(1, ledger.Skipped);
        Assert.Equal(EntryKind.Inflow, ledger.Entries[0].Kind);
        Assert.Equal("Cartão refeição", ledger.Entries[1].Account);
    }

    [Fact]
    public void Read_RefusesSomethingElse()
    {
        Assert.Null(LedgerFile.Read("{\"app\":\"obol\",\"entries\":[]}"));
        Assert.Null(LedgerFile.Read("not a ledger"));
    }

    [Fact]
    public async Task Reset_LeavesTheFourStarterAccounts()
    {
        var harness = new Harness();
        var demo = await harness.DemoAsync();
        Assert.True((await harness.BackupService.ResetAsync(demo)).IsSuccess);

        var accounts = (await harness.AccountService.ListAsync(demo)).Result!;
        Assert.Equal([AccountKind.Current, AccountKind.Savings, AccountKind.Investments, AccountKind.MealCard], accounts.Select(static account => account.Kind));
        Assert.All(accounts, static account => Assert.Equal(0m, account.Balance));
        Assert.Empty((await harness.ProjectService.ListAsync(demo)).Result!.Projects);
    }
}
