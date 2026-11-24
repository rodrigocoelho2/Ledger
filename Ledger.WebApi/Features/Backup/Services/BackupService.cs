using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Backup.Models.DTOs;
using Ledger.WebApi.Features.Demo.Services;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Repositories;
using Ledger.WebApi.Features.Projects.Repositories;

namespace Ledger.WebApi.Features.Backup.Services;

/// <summary>
/// Getting a ledger out of the app and back in.
/// </summary>
internal interface IBackupService
{
    /// <summary>Every entry as a CSV file, oldest first.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    Task<ServiceApiResponse<ExportDTO>> CsvAsync(string? userId);

    /// <summary>The whole ledger (accounts, projects, caps, entries) as a JSON file.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    Task<ServiceApiResponse<ExportDTO>> JsonAsync(string? userId);

    /// <summary>Reads a CSV or a JSON backup into the ledger.</summary>
    /// <param name="item">The file, and whether it replaces what is there.</param>
    /// <returns>What was added.</returns>
    Task<ServiceApiResponse<ImportResultDTO>> ImportAsync(ImportRequest item);

    /// <summary>Start over: erases every entry, project, account and cap, and puts the four starting accounts back.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The user's id.</returns>
    Task<ServiceApiResponse<string>> ResetAsync(string? userId);
}

/// <summary>
/// Backup business logic.
/// </summary>
/// <param name="entryRepository">The entries.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="projectRepository">The projects.</param>
/// <param name="capRepository">The spending caps.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class BackupService(
    IEntryRepository entryRepository,
    IAccountRepository accountRepository,
    IProjectRepository projectRepository,
    ICapRepository capRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IBackupService
{
    /// <summary>The largest file an import reads, in characters.</summary>
    public const int MaxFileLength = 8_000_000;

    private const string FieldFile = "File";

    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly ICapRepository _capRepository = capRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ExportDTO>> CsvAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<ExportDTO>(userId);
        }

        var ledger = await LoadAsync(id);
        return ServiceApiResponse<ExportDTO>.Success(new ExportDTO
        {
            FileName = $"ledger-{_clock.Today:yyyy-MM-dd}.csv",
            ContentType = "text/csv",
            Content = LedgerFile.WriteCsv(ledger.Entries, Globalization.Language),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ExportDTO>> JsonAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<ExportDTO>(userId);
        }

        var ledger = await LoadAsync(id);
        return ServiceApiResponse<ExportDTO>.Success(new ExportDTO
        {
            FileName = $"ledger-{_clock.Today:yyyy-MM-dd}.json",
            ContentType = "application/json",
            Content = LedgerFile.WriteJson(ledger, new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc))),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ImportResultDTO>> ImportAsync(ImportRequest item)
    {
        if (!TryUser(item.UserId, out var id))
        {
            return await MissingAsync<ImportResultDTO>(item.UserId);
        }

        var file = item.Content is { Length: <= MaxFileLength } ? LedgerFile.Read(item.Content) : null;
        if (file is null)
        {
            return await ProblemAsync<ImportResultDTO>(FieldFile, nameof(TranslationKeys.FileInvalid));
        }

        if (file.Entries.Count == 0 && file.Accounts.Count == 0)
        {
            return await ProblemAsync<ImportResultDTO>(FieldFile, nameof(TranslationKeys.FileEmpty));
        }

        if (item.Replace)
        {
            await _entryRepository.ResetAsync(id);
        }

        var result = new ImportResultDTO { Skipped = file.Skipped };

        // The accounts: the ones the file describes, then any an entry names that nobody has yet.
        var accounts = (await _accountRepository.ListAsync(id)).ToDictionary(static account => account.Name, static account => account.AccountUniqueID, StringComparer.OrdinalIgnoreCase);
        var firstAccount = (await _accountRepository.ListAsync(id)).FirstOrDefault()?.AccountUniqueID;
        foreach (var account in file.Accounts)
        {
            await EnsureAccountAsync(account.Name, account.Kind, account.OpeningBalance);
        }

        foreach (var name in file.Entries.SelectMany(static entry => new[] { entry.Account, entry.ToAccount ?? string.Empty }).Where(static name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await EnsureAccountAsync(name, null, 0m);
        }

        // Entries that name no account go to the person's first one (or to a new everyday account).
        if (firstAccount is null && file.Entries.Any(static entry => entry.Account.Length == 0))
        {
            await EnsureAccountAsync(StarterSeeder.Accounts[0].English, AccountKind.Current, 0m);
        }

        var projects = (await _projectRepository.ListAsync(id)).ToDictionary(static project => project.Name, static project => project.ProjectUniqueID, StringComparer.OrdinalIgnoreCase);
        foreach (var project in file.Projects)
        {
            await EnsureProjectAsync(project.Name, project.TargetSale, project.WorksBudget, project.AgreedPrice);
        }

        foreach (var name in file.Entries.Where(static entry => entry.Project is not null).Select(static entry => entry.Project!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await EnsureProjectAsync(name, null, null, null);
        }

        foreach (var (month, amount) in file.Caps)
        {
            await _capRepository.SaveAsync(id, month, Math.Min(amount, MaxAmount));
        }

        // The same file read twice adds nothing the second time: an entry is skipped when one just like it is
        // already there, counting how many of each the ledger has and the file brings.
        var rows = file.Entries.Where(static entry => entry.Amount <= MaxAmount).Select(ToRow).ToList();
        var existing = (await _entryRepository.ListAsync(id)).GroupBy(Fingerprint).ToDictionary(static group => group.Key, static group => group.Count());
        var fresh = new List<EntryRow>();
        foreach (var row in rows)
        {
            var key = Fingerprint(row);
            if (existing.TryGetValue(key, out var left) && left > 0)
            {
                existing[key] = left - 1;
                result.AlreadyThere++;
            }
            else
            {
                fresh.Add(row);
            }
        }

        result.Added = await _entryRepository.ImportAsync(id, fresh);
        return ServiceApiResponse<ImportResultDTO>.Success(result);

        async Task EnsureAccountAsync(string name, AccountKind? kind, decimal openingBalance)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0 || accounts.ContainsKey(trimmed))
            {
                return;
            }

            trimmed = trimmed.Length <= 60 ? trimmed : trimmed[..60];
            var opening = Math.Clamp(decimal.Round(openingBalance, 2, MidpointRounding.AwayFromZero), -MaxAmount, MaxAmount);
            if (await _accountRepository.SaveAsync(id, null, trimmed, kind ?? LedgerFile.GuessKind(trimmed), opening) is { } created)
            {
                accounts[trimmed] = created;
                firstAccount ??= created;
                result.AccountsCreated++;
            }
        }

        async Task EnsureProjectAsync(string name, decimal? targetSale, decimal? worksBudget, decimal? agreedPrice)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0 || projects.ContainsKey(trimmed))
            {
                return;
            }

            trimmed = trimmed.Length <= EntryService.ProjectNameLength ? trimmed : trimmed[..EntryService.ProjectNameLength];
            if (await _projectRepository.SaveAsync(id, null, trimmed, targetSale, worksBudget, agreedPrice) is { } created)
            {
                projects[trimmed] = created;
                result.ProjectsCreated++;
            }
        }

        EntryRow ToRow(FileEntry entry) => new()
        {
            Kind = (byte)entry.Kind,
            Scope = (byte)entry.Scope,
            Name = entry.Name,
            Amount = entry.Amount,
            EntryDate = entry.Day.ToDateTime(TimeOnly.MinValue),
            Category = entry.Category,
            AccountUniqueID = entry.Account.Length == 0 ? firstAccount : accounts.TryGetValue(entry.Account.Trim(), out var account) ? account : firstAccount,
            ToAccountUniqueID = entry.ToAccount is not null && accounts.TryGetValue(entry.ToAccount.Trim(), out var to) ? to : null,
            ProjectUniqueID = entry.Project is not null && projects.TryGetValue(entry.Project.Trim(), out var project) ? project : null,
            Nights = (short?)entry.Nights,
            Channel = entry.Channel,
            PlanKey = entry.Plan,
            PlanPart = (short?)entry.PlanPart,
            PlanParts = (short?)entry.PlanParts,
            ExternalID = entry.External,
            ToExternalID = entry.ExternalTo,
        };
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> ResetAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<string>(userId);
        }

        await _entryRepository.ResetAsync(id);
        await new StarterSeeder(_accountRepository).SeedAsync(id, Globalization.Language);
        return ServiceApiResponse<string>.Success(Id(id));
    }

    /// <summary>What makes two entries "the same line" for an import: day, kind, scope, words, amount and accounts.</summary>
    /// <param name="row">The entry.</param>
    /// <returns>The key.</returns>
    internal static string Fingerprint(EntryRow row) =>
        string.Join('|', row.DayText, row.Kind, row.Scope, row.Name.Trim().ToUpperInvariant(), row.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), row.AccountUniqueID, row.ToAccountUniqueID);

    private async Task<FileLedger> LoadAsync(Guid userId)
    {
        var accounts = await _accountRepository.ListAsync(userId);
        var projects = await _projectRepository.ListAsync(userId);
        var accountNames = accounts.ToDictionary(static account => account.AccountUniqueID, static account => account.Name);
        var projectNames = projects.ToDictionary(static project => project.ProjectUniqueID, static project => project.Name);

        var ledger = new FileLedger();
        ledger.Accounts.AddRange(accounts.Select(static account => new FileAccount(account.Name, (AccountKind)account.Kind, account.OpeningBalance)));
        ledger.Projects.AddRange(projects.Select(static project => new FileProject(project.Name, project.TargetSale, project.WorksBudget, project.AgreedPrice)));
        foreach (var (month, amount) in await _capRepository.ListAsync(userId))
        {
            ledger.Caps[month] = amount;
        }

        ledger.Entries.AddRange((await _entryRepository.ListAsync(userId))
            .AsEnumerable()
            .Reverse()
            .Select(row => new FileEntry(
                row.Day,
                (EntryKind)row.Kind,
                (EntryScope)row.Scope,
                row.Name,
                row.Category,
                row.Amount,
                row.AccountUniqueID is { } account && accountNames.TryGetValue(account, out var accountName) ? accountName : string.Empty,
                row.ToAccountUniqueID is { } to && accountNames.TryGetValue(to, out var toName) ? toName : null,
                row.ProjectUniqueID is { } project && projectNames.TryGetValue(project, out var projectName) ? projectName : null,
                row.Nights,
                row.Channel,
                row.PlanKey,
                row.PlanPart,
                row.PlanParts,
                row.ExternalID,
                row.ToExternalID)));
        return ledger;
    }
}
