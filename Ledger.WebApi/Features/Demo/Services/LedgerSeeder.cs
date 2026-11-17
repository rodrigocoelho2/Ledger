using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Overview.Repositories;
using Ledger.WebApi.Features.Projects.Repositories;
using Ledger.WebApi.Features.Settings.Repositories;
using Ledger.WebApi.Features.Users.Repositories;
using Ledger.WebApi.Features.Users.Services;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Demo.Services;

/// <summary>
/// Gives a new user (or one who started over) something to begin with.
/// </summary>
internal interface ILedgerSeeder
{
    /// <summary>
    /// Fills in a user's empty ledger.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="language">The language for the names of the accounts ("en" or "pt").</param>
    /// <returns>A task that completes when the ledger is ready.</returns>
    Task SeedAsync(Guid userId, string? language = null);
}

/// <summary>
/// The normal start: four empty accounts (the everyday one, savings, investments and a meal card), which the person
/// renames, reorders or deletes as they like. The optional modules start switched off.
/// </summary>
/// <param name="accountRepository">The accounts.</param>
internal sealed class StarterSeeder(IAccountRepository accountRepository) : ILedgerSeeder
{
    /// <summary>The accounts every ledger starts with, in order: the kind and its English and Portuguese names.</summary>
    internal static readonly (AccountKind Kind, string English, string Portuguese)[] Accounts =
    [
        (AccountKind.Current, "Main account", "Conta principal"),
        (AccountKind.Savings, "Savings", "Poupança"),
        (AccountKind.Investments, "Investments", "Investimentos"),
        (AccountKind.MealCard, "Meal card", "Cartão refeição"),
    ];

    /// <inheritdoc />
    public async Task SeedAsync(Guid userId, string? language = null)
    {
        if ((await accountRepository.ListAsync(userId)).Count > 0)
        {
            return;
        }

        var portuguese = Languages.IsPortuguese(language);
        foreach (var (kind, english, portugueseName) in Accounts)
        {
            await accountRepository.SaveAsync(userId, null, portuguese ? portugueseName : english, kind, 0m);
        }
    }
}

/// <summary>
/// Demo mode: the sample ledger of <see cref="DemoData"/>, with both optional modules switched on.
/// </summary>
/// <param name="accountRepository">The accounts.</param>
/// <param name="projectRepository">The projects.</param>
/// <param name="entryRepository">The entries.</param>
/// <param name="capRepository">The spending caps.</param>
/// <param name="settingRepository">The settings.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
internal sealed class DemoSeeder(
    IAccountRepository accountRepository,
    IProjectRepository projectRepository,
    IEntryRepository entryRepository,
    ICapRepository capRepository,
    ISettingRepository settingRepository,
    IAppClock clock) : ILedgerSeeder
{
    /// <inheritdoc />
    public async Task SeedAsync(Guid userId, string? language = null)
    {
        if ((await accountRepository.ListAsync(userId)).Count > 0)
        {
            return;
        }

        var demo = DemoData.Generate(clock.Today);

        var accounts = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var account in demo.Accounts)
        {
            if (await accountRepository.SaveAsync(userId, null, account.Name, account.Kind, account.OpeningBalance) is { } id)
            {
                accounts[account.Name] = id;
            }
        }

        var projects = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var project in demo.Projects)
        {
            if (await projectRepository.SaveAsync(userId, null, project, null, null) is { } id)
            {
                projects[project] = id;
            }
        }

        foreach (var (month, amount) in demo.Caps)
        {
            await capRepository.SaveAsync(userId, month, amount);
        }

        await entryRepository.ImportAsync(userId, [.. demo.Entries.Select(entry => new EntryRow
        {
            Kind = (byte)entry.Kind,
            Scope = (byte)entry.Scope,
            Name = entry.Name,
            Amount = entry.Amount,
            EntryDate = entry.Day.ToDateTime(TimeOnly.MinValue),
            Category = entry.Category,
            AccountUniqueID = accounts.TryGetValue(entry.Account, out var account) ? account : null,
            ToAccountUniqueID = entry.ToAccount is not null && accounts.TryGetValue(entry.ToAccount, out var to) ? to : null,
            ProjectUniqueID = entry.Project is not null && projects.TryGetValue(entry.Project, out var project) ? project : null,
            Nights = (short?)entry.Nights,
            Channel = entry.Channel,
        })]);

        await settingRepository.SaveAsync(userId, Languages.Normalize(language), rentalEnabled: true, flipsEnabled: true);
    }
}

/// <summary>
/// Demo mode: creates the "demo" user when the WebApi starts.
/// </summary>
/// <param name="userRepository">The users.</param>
/// <param name="seeder">Fills in the sample ledger.</param>
/// <param name="options">Module options (the demo password).</param>
/// <param name="logger">Logger.</param>
internal sealed class DemoStartup(
    IUserRepository userRepository,
    ILedgerSeeder seeder,
    IOptions<LedgerOptions> options,
    ILogger<DemoStartup> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await userRepository.GetByNameAsync(UserService.DemoUserName) is not null)
        {
            return;
        }

        var user = await userRepository.CreateAsync(UserService.DemoUserName, PasswordHasher.Hash(options.Value.DemoPassword), Guid.NewGuid().ToString("N"));
        if (user is not null)
        {
            await seeder.SeedAsync(user.UserUniqueID);
            logger.LogInformation("Demo mode: sign in as {UserName} / {Password}", UserService.DemoUserName, options.Value.DemoPassword);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
