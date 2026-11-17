using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Accounts.Services;
using Ledger.WebApi.Features.Backup.Services;
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
using Microsoft.AspNetCore.Http;

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
        var options = Microsoft.Extensions.Options.Options.Create(new LedgerOptions { ApiKey = "test-key", TimeZone = "Europe/Lisbon" });
        Clock = new AppClock(time, options);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = language;
        var globalization = new GlobalizationHelper(new HttpContextAccessor { HttpContext = context });

        Accounts = new MemoryAccountRepository(Store);
        Entries = new MemoryEntryRepository(Store, time);
        Projects = new MemoryProjectRepository(Store, time);
        Caps = new MemoryCapRepository(Store);
        Settings = new MemorySettingRepository(Store);

        AccountService = new AccountService(Accounts, globalization);
        EntryService = new EntryService(Entries, Accounts, Projects, Clock, globalization);
        OverviewService = new OverviewService(Entries, Accounts, Projects, Caps, Clock, globalization);
        RentalService = new RentalService(Entries, Accounts, Projects, Clock, globalization);
        ProjectService = new ProjectService(Projects, Entries, Accounts, Clock, globalization);
        BackupService = new BackupService(Entries, Accounts, Projects, Caps, Clock, globalization);
    }

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
