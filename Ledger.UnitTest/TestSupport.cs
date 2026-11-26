using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features;
using Ledger.WebApi.Features.Accounts.Http;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Accounts.Services;
using Ledger.WebApi.Features.Backup.Services;
using Ledger.WebApi.Features.Banks.Http;
using Ledger.WebApi.Features.Banks.Repositories;
using Ledger.WebApi.Features.Banks.Services;
using Ledger.WebApi.Features.Demo.Services;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Repositories;
using Ledger.WebApi.Features.Overview.Services;
using Ledger.WebApi.Features.Projects.Repositories;
using Ledger.WebApi.Features.Projects.Services;
using Ledger.WebApi.Features.Rental.Services;
using Ledger.WebApi.Features.Settings.Repositories;
using Ledger.WebApi.Features.Users.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ledger.UnitTest;

/// <summary>
/// A clock that never moves.
/// </summary>
/// <param name="now">The moment it always says.</param>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// Every service of the WebApi over the in-memory repositories, with a fixed clock: 17 November 2026, so the demo
/// ledger's last full month is October 2026.
/// </summary>
internal sealed class Harness
{
    /// <summary>The moment the tests run at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Initializes a new instance of the <see cref="Harness"/> class.</summary>
    /// <param name="language">The Accept-Language of the "request" ("en" or "pt").</param>
    public Harness(string language = "en")
    {
        var time = new FixedTime(Now);
        Options = new LedgerOptions
        {
            ApiKey = "test-key",
            TimeZone = "Europe/Lisbon",
            EnableBankingAppId = "test-app",
            EnableBankingKey = "fake",
            ObolUrl = "http://obol",
            ObolApiKey = "obol-key",
        };
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        Clock = new AppClock(time, options);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = language;
        var globalization = new GlobalizationHelper(new HttpContextAccessor { HttpContext = context });

        Accounts = new MemoryAccountRepository(Store, Clock);
        Entries = new MemoryEntryRepository(Store, time);
        Projects = new MemoryProjectRepository(Store, time);
        Caps = new MemoryCapRepository(Store);
        Settings = new MemorySettingRepository(Store);
        Users = new MemoryUserRepository(Store, time);
        BankLinks = new MemoryBankRepository(Store, time);

        Valuation = new ValuationService(Accounts, Users, Obol, options, Clock, NullLogger<ValuationService>.Instance);
        AccountService = new AccountService(Accounts, Valuation, options, globalization);
        BankService = new BankService(BankLinks, Accounts, Entries, Bank, Users, options, Clock, globalization);
        MonthService = new MonthService(Users, Entries);
        EntryService = new EntryService(Entries, Accounts, Projects, MonthService, Clock, globalization);
        OverviewService = new OverviewService(Entries, Accounts, Projects, Caps, MonthService, Clock, globalization);
        RentalService = new RentalService(Entries, Accounts, Projects, Clock, globalization);
        ProjectService = new ProjectService(Projects, Entries, Accounts, Clock, globalization);
        BackupService = new BackupService(Entries, Accounts, Projects, Caps, Clock, globalization);
    }

    /// <summary>Gets the options (bank and Obol switched on, over the fakes).</summary>
    public LedgerOptions Options { get; }

    /// <summary>Gets the fake Obol.</summary>
    public FakeObol Obol { get; } = new();

    /// <summary>Gets the fake Enable Banking.</summary>
    public FakeBank Bank { get; } = new();

    /// <summary>Gets the user repository.</summary>
    public IUserRepository Users { get; }

    /// <summary>Gets the bank link repository.</summary>
    public IBankRepository BankLinks { get; }

    /// <summary>Gets the valuation service.</summary>
    public IValuationService Valuation { get; }

    /// <summary>Gets the bank service.</summary>
    public IBankService BankService { get; }

    /// <summary>Gets the shared in-memory store.</summary>
    public MemoryStore Store { get; } = new();

    /// <summary>Gets the clock.</summary>
    public IAppClock Clock { get; }

    /// <summary>Gets today, in Lisbon.</summary>
    public DateOnly Today => Clock.Today;

    /// <summary>Gets the account repository.</summary>
    public IAccountRepository Accounts { get; }

    /// <summary>Gets the entry repository.</summary>
    public IEntryRepository Entries { get; }

    /// <summary>Gets the project repository.</summary>
    public IProjectRepository Projects { get; }

    /// <summary>Gets the cap repository.</summary>
    public ICapRepository Caps { get; }

    /// <summary>Gets the setting repository.</summary>
    public ISettingRepository Settings { get; }

    /// <summary>Gets the account service.</summary>
    public IAccountService AccountService { get; }

    /// <summary>Gets the entry service.</summary>
    public IEntryService EntryService { get; }

    /// <summary>Gets where the months begin.</summary>
    public IMonthService MonthService { get; }

    /// <summary>Gets the overview service.</summary>
    public IOverviewService OverviewService { get; }

    /// <summary>Gets the rental service.</summary>
    public IRentalService RentalService { get; }

    /// <summary>Gets the project service.</summary>
    public IProjectService ProjectService { get; }

    /// <summary>Gets the backup service.</summary>
    public IBackupService BackupService { get; }

    /// <summary>A user with the four starter accounts.</summary>
    /// <returns>The user's id, as the services take it.</returns>
    public async Task<string> StarterAsync()
    {
        var id = Guid.NewGuid();
        await new StarterSeeder(Accounts).SeedAsync(id, "en");
        return id.ToString("N");
    }

    /// <summary>A user with the sample ledger.</summary>
    /// <returns>The user's id, as the services take it.</returns>
    public async Task<string> DemoAsync()
    {
        var id = Guid.NewGuid();
        await new DemoSeeder(Accounts, Projects, Entries, Caps, Settings, Clock).SeedAsync(id, "en");
        return id.ToString("N");
    }

    /// <summary>The id of an account, by name.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="name">The account's name.</param>
    /// <returns>The id.</returns>
    public async Task<string> AccountIdAsync(string userId, string name) =>
        (await AccountService.ListAsync(userId)).Result!.Single(account => account.Name == name).Id;
}

/// <summary>Entries made up for the pure maths.</summary>
internal static class Make
{
    /// <summary>An entry.</summary>
    /// <param name="kind">Money in, out or moved.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="day">The day.</param>
    /// <param name="category">The category key.</param>
    /// <param name="scope">The part of life.</param>
    /// <param name="account">The account.</param>
    /// <param name="to">The account a transfer goes to.</param>
    /// <param name="nights">The nights of a booking.</param>
    /// <returns>The row.</returns>
    public static EntryRow Entry(EntryKind kind, decimal amount, DateOnly day, string? category = null, EntryScope scope = EntryScope.Personal, Guid? account = null, Guid? to = null, short? nights = null) => new()
    {
        EntryUniqueID = Guid.NewGuid(),
        Kind = (byte)kind,
        Scope = (byte)scope,
        Name = category ?? "transfer",
        Amount = amount,
        EntryDate = day.ToDateTime(TimeOnly.MinValue),
        Category = kind == EntryKind.Transfer ? null : category ?? (kind == EntryKind.Inflow ? "other-income" : "other"),
        AccountUniqueID = account,
        ToAccountUniqueID = to,
        Nights = nights,
    };
}

/// <summary>An Obol that answers from a dictionary.</summary>
internal sealed class FakeObol : IObolClient
{
    /// <summary>Gets the values by user name (null: no data yet).</summary>
    public Dictionary<string, decimal?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets an error to throw.</summary>
    public string? Error { get; set; }

    /// <inheritdoc />
    public Task<decimal?> ValueAsync(string userName, CancellationToken cancellationToken = default) =>
        Error is not null ? throw new ObolException(Error)
        : Values.TryGetValue(userName, out var value) ? Task.FromResult(value)
        : throw new ObolException($"no user \"{userName}\" in Obol");
}

/// <summary>An Enable Banking that answers from lists.</summary>
internal sealed class FakeBank : IEnableBankingClient
{
    /// <summary>Gets the banks.</summary>
    public List<EbAspsp> Banks { get; } = [new() { Name = "Crédito Agrícola", Country = "PT", MaximumConsentValidity = 90 * 86400, PsuTypes = ["personal"] }];

    /// <summary>Gets the registered redirect addresses.</summary>
    public List<string> Redirects { get; } = ["https://ledger.example/bank/callback"];

    /// <summary>Gets or sets the session a code turns into.</summary>
    public EbSession Session { get; set; } = new()
    {
        SessionId = "session-1",
        Aspsp = new EbSessionAspsp { Name = "Crédito Agrícola", Country = "PT" },
        Access = new EbAccess { ValidUntil = new DateTimeOffset(2027, 2, 15, 0, 0, 0, TimeSpan.Zero) },
        Accounts = [new EbAccount { Uid = "acc-1", Name = "Conta à ordem", AccountId = new EbAccountId { Iban = "PT50 0045 0000 0000 0000 0123 4" } }],
    };

    /// <summary>Gets the transactions of every account (unless the account has its own list).</summary>
    public List<EbTransaction> Transactions { get; } = [];

    /// <summary>Gets the transactions of one account, by its uid.</summary>
    public Dictionary<string, List<EbTransaction>> ByAccount { get; } = [];

    /// <summary>Gets the booked balance of one account, by its uid.</summary>
    public Dictionary<string, decimal> BalanceOf { get; } = [];

    /// <summary>Gets or sets the booked balance.</summary>
    public decimal? Balance { get; set; }

    /// <summary>Gets or sets an error to throw on transactions.</summary>
    public string? Error { get; set; }

    /// <summary>Gets the date ranges asked for.</summary>
    public List<(DateOnly From, DateOnly To)> Asked { get; } = [];

    /// <summary>Gets or sets the last state sent to /auth.</summary>
    public string? LastState { get; set; }

    /// <summary>Gets or sets the last redirect sent to /auth.</summary>
    public string? LastRedirect { get; set; }

    /// <summary>Gets the sessions ended.</summary>
    public List<string> Ended { get; } = [];

    /// <inheritdoc />
    public Task<List<EbAspsp>> BanksAsync(string country, CancellationToken cancellationToken = default) => Task.FromResult(Banks);

    /// <inheritdoc />
    public Task<List<string>> RedirectUrlsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Redirects);

    /// <inheritdoc />
    public Task<string> StartAsync(string bank, string country, DateTimeOffset validUntil, string redirectUrl, string state, CancellationToken cancellationToken = default)
    {
        LastState = state;
        LastRedirect = redirectUrl;
        return Task.FromResult($"https://bank.example/auth?state={state}");
    }

    /// <inheritdoc />
    public Task<EbSession> SessionAsync(string code, CancellationToken cancellationToken = default) =>
        code == "good" ? Task.FromResult(Session) : throw new BankException("422 invalid code");

    /// <inheritdoc />
    public Task EndAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        Ended.Add(sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<EbBalance>> BalancesAsync(string accountUid, CancellationToken cancellationToken = default) =>
        Task.FromResult<List<EbBalance>>((BalanceOf.TryGetValue(accountUid, out var own) ? own : Balance) is { } balance
            ? [new EbBalance { BalanceType = "ITAV", BalanceAmount = new EbAmount { Amount = (balance + 100).ToString(System.Globalization.CultureInfo.InvariantCulture) } },
               new EbBalance { BalanceType = "CLBD", BalanceAmount = new EbAmount { Amount = balance.ToString(System.Globalization.CultureInfo.InvariantCulture) } }]
            : []);

    /// <inheritdoc />
    public Task<List<EbTransaction>> TransactionsAsync(string accountUid, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        Asked.Add((from, to));
        if (Error is not null)
        {
            throw new BankException(Error);
        }

        return Task.FromResult((ByAccount.TryGetValue(accountUid, out var own) ? own : Transactions)
            .Where(transaction => DateOnly.ParseExact(transaction.BookingDate!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) is var day && day >= from && day <= to)
            .ToList());
    }

    /// <summary>A booked transaction.</summary>
    /// <param name="id">The bank's reference.</param>
    /// <param name="amount">Positive in, negative out.</param>
    /// <param name="day">The day.</param>
    /// <param name="party">Who paid or was paid.</param>
    /// <param name="text">The description.</param>
    /// <returns>The transaction.</returns>
    public static EbTransaction Move(string? id, decimal amount, DateOnly day, string? party = null, string? text = null) => new()
    {
        EntryReference = id,
        TransactionAmount = new EbAmount { Amount = Math.Abs(amount).ToString(System.Globalization.CultureInfo.InvariantCulture), Currency = "EUR" },
        CreditDebitIndicator = amount > 0 ? "CRDT" : "DBIT",
        Status = "BOOK",
        BookingDate = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        Creditor = amount < 0 && party is not null ? new EbParty { Name = party } : null,
        Debtor = amount > 0 && party is not null ? new EbParty { Name = party } : null,
        RemittanceInformation = text is null ? null : [text],
    };
}
