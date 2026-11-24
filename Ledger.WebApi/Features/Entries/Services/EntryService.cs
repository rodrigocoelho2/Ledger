using Ledger.Library.Calendar;
using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Projects.Repositories;

namespace Ledger.WebApi.Features.Entries.Services;

/// <summary>
/// The entries.
/// </summary>
internal interface IEntryService
{
    /// <summary>The entries of a month, optionally of one part of life and one category.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <param name="scope">The part of life (empty: all).</param>
    /// <param name="category">A category key, or "transfers" (empty: all).</param>
    /// <returns>The entries.</returns>
    Task<ServiceApiResponse<EntryMonthDTO>> MonthAsync(string? userId, string? month, EntryScope? scope, string? category);

    /// <summary>Gets one entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>The entry.</returns>
    Task<ServiceApiResponse<EntryDTO>> GetAsync(string? userId, string? identifier);

    /// <summary>Adds or changes an entry.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The entry, or validation problems.</returns>
    Task<ServiceApiResponse<EntryDTO>> SaveAsync(EntrySaveRequest item);

    /// <summary>Deletes an entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>The deleted id.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier);

    /// <summary>Deletes every instalment of a plan, past and future.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="planId">The plan.</param>
    /// <returns>How many instalments were deleted.</returns>
    Task<ServiceApiResponse<int>> DeletePlanAsync(string? userId, string? planId);

    /// <summary>Reads a quick-add line and fills in the rest from the last entry written with the same words.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="text">The line ("9.80 lunch").</param>
    /// <returns>What was understood.</returns>
    Task<ServiceApiResponse<QuickAddDTO>> QuickAsync(string? userId, string? text);

    /// <summary>Marks entries brought from the bank as looked at.</summary>
    /// <param name="item">The entries (none: all of them).</param>
    /// <returns>How many changed.</returns>
    Task<ServiceApiResponse<int>> ReviewedAsync(EntryReviewedRequest item);
}

/// <summary>
/// Entry business logic.
/// </summary>
/// <param name="entryRepository">The entries.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="projectRepository">The projects.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class EntryService(
    IEntryRepository entryRepository,
    IAccountRepository accountRepository,
    IProjectRepository projectRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IEntryService
{
    /// <summary>The longest description of an entry.</summary>
    public const int NameLength = 120;

    /// <summary>The longest channel of a booking.</summary>
    public const int ChannelLength = 40;

    /// <summary>The longest name of a project.</summary>
    public const int ProjectNameLength = 80;

    /// <summary>The filter value that lists only the transfers.</summary>
    public const string TransfersFilter = "transfers";

    /// <summary>The filter value that lists only what came from the bank and waits to be looked at.</summary>
    public const string ReviewFilter = "review";

    private const string FieldName = "Name";
    private const string FieldKind = "Kind";
    private const string FieldAmount = "Amount";
    private const string FieldDate = "Date";
    private const string FieldCategory = "Category";
    private const string FieldAccount = "AccountId";
    private const string FieldToAccount = "ToAccountId";
    private const string FieldProject = "ProjectId";
    private const string FieldNights = "Nights";
    private const string FieldChannel = "Channel";
    private const string FieldInstalments = "Instalments";
    private const string FieldText = "Text";

    private static readonly DateOnly FirstDay = new(1990, 1, 1);

    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<EntryMonthDTO>> MonthAsync(string? userId, string? month, EntryScope? scope, string? category)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<EntryMonthDTO>(userId);
        }

        var start = Months.Parse(month, _clock.Today);
        var rows = await _entryRepository.ListAsync(id, start, Months.End(start), scope is { } part && Enum.IsDefined(part) ? part : null);
        var mapper = new EntryMapper(await _accountRepository.ListAsync(id), await _projectRepository.ListAsync(id));

        var wanted = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var listed = wanted is null
            ? rows
            : wanted.Equals(TransfersFilter, StringComparison.OrdinalIgnoreCase)
                ? rows.Where(static row => row.Kind == (byte)EntryKind.Transfer).ToList()
                : wanted.Equals(ReviewFilter, StringComparison.OrdinalIgnoreCase)
                    ? rows.Where(static row => row.Review == true).ToList()
                    : rows.Where(row => string.Equals(row.Category, wanted, StringComparison.OrdinalIgnoreCase)).ToList();

        return ServiceApiResponse<EntryMonthDTO>.Success(new EntryMonthDTO
        {
            Month = Months.Text(start),
            Entries = mapper.Map(listed),
            Total = rows.Count,
            In = listed.Where(static row => row.Kind == (byte)EntryKind.Inflow).Sum(static row => row.Amount),
            Out = listed.Where(static row => row.Kind == (byte)EntryKind.Outflow).Sum(static row => row.Amount),
            Categories = rows.Where(static row => row.Category is not null)
                .GroupBy(static row => row.Category!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => group.Key)
                .ToList(),
            HasTransfers = rows.Any(static row => row.Kind == (byte)EntryKind.Transfer),
            ReviewCount = rows.Count(static row => row.Review == true),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<EntryDTO>> GetAsync(string? userId, string? identifier)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(identifier, out var entryId))
        {
            return await MissingAsync<EntryDTO>(identifier);
        }

        var row = await _entryRepository.GetAsync(id, entryId);
        if (row is null)
        {
            return await MissingAsync<EntryDTO>(identifier);
        }

        var mapper = new EntryMapper(await _accountRepository.ListAsync(id), await _projectRepository.ListAsync(id));
        return ServiceApiResponse<EntryDTO>.Success(mapper.Map(row));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<EntryDTO>> SaveAsync(EntrySaveRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<EntryDTO>(item.UserId);
        }

        Guid? entryId = null;
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (!Guid.TryParse(item.Id, out var parsed))
            {
                return await MissingAsync<EntryDTO>(item.Id);
            }

            entryId = parsed;
        }

        var accounts = await _accountRepository.ListAsync(userId);
        var errors = new List<ApiValidationError>();

        // Kind and scope
        if (item.Kind is not { } kind || !Enum.IsDefined(kind))
        {
            errors.Add(await ErrorAsync(FieldKind, nameof(TranslationKeys.RequiredField)));
            kind = EntryKind.Outflow;
        }

        var scope = item.Scope is { } part && Enum.IsDefined(part) ? part : EntryScope.Personal;

        // What it was
        var name = (item.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }
        else if (name.Length > NameLength)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.TooLong), NameLength));
        }

        // How much, and when
        var amount = decimal.Round(item.Amount ?? 0m, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0 || amount > MaxAmount)
        {
            errors.Add(await ErrorAsync(FieldAmount, nameof(TranslationKeys.AmountInvalid)));
        }

        var day = item.Date ?? _clock.Today;
        if (day < FirstDay || day > _clock.Today.AddYears(10))
        {
            errors.Add(await ErrorAsync(FieldDate, nameof(TranslationKeys.DateInvalid)));
        }

        // The accounts
        var account = FindAccount(item.AccountId);
        if (account is null)
        {
            errors.Add(await ErrorAsync(FieldAccount, string.IsNullOrWhiteSpace(item.AccountId) ? nameof(TranslationKeys.RequiredField) : nameof(TranslationKeys.AccountUnknown)));
        }

        Guid? toAccount = null;
        string? category = null;
        if (kind == EntryKind.Transfer)
        {
            toAccount = FindAccount(item.ToAccountId);
            if (toAccount is null)
            {
                errors.Add(await ErrorAsync(FieldToAccount, string.IsNullOrWhiteSpace(item.ToAccountId) ? nameof(TranslationKeys.RequiredField) : nameof(TranslationKeys.AccountUnknown)));
            }
            else if (toAccount == account)
            {
                var message = await Globalization.TranslateAsync(nameof(TranslationKeys.SameAccount));
                errors.Add(new ApiValidationError(FieldToAccount, message));
            }
        }
        else
        {
            var found = Categories.Find(string.IsNullOrWhiteSpace(item.Category) ? Categories.Fallback(kind) : item.Category.Trim());
            if (found is null || found.Kind != kind)
            {
                errors.Add(await ErrorAsync(FieldCategory, nameof(TranslationKeys.CategoryUnknown)));
            }
            else
            {
                category = found.Key;
            }
        }

        // A rental booking: its nights and where it came from
        short? nights = null;
        string? channel = null;
        if (kind == EntryKind.Inflow && scope == EntryScope.Rental && category == Categories.Bookings)
        {
            if (item.Nights is { } count)
            {
                if (count is < 1 or > 365)
                {
                    errors.Add(await ErrorAsync(FieldNights, nameof(TranslationKeys.OutOfRange), 1, 365));
                }
                else
                {
                    nights = (short)count;
                }
            }

            channel = string.IsNullOrWhiteSpace(item.Channel) ? null : item.Channel.Trim();
            if (channel is { Length: > ChannelLength })
            {
                errors.Add(await ErrorAsync(FieldChannel, nameof(TranslationKeys.TooLong), ChannelLength));
            }
        }

        // A buy-and-sell project: one of the person's, or a new one by name
        Guid? project = null;
        var projectName = (item.ProjectName ?? string.Empty).Trim();
        if (scope == EntryScope.Flips && kind != EntryKind.Transfer)
        {
            if (!string.IsNullOrWhiteSpace(item.ProjectId))
            {
                var projects = await _projectRepository.ListAsync(userId);
                project = Guid.TryParse(item.ProjectId, out var projectId) && projects.Any(candidate => candidate.ProjectUniqueID == projectId) ? projectId : null;
                if (project is null)
                {
                    errors.Add(await ErrorAsync(FieldProject, nameof(TranslationKeys.ProjectUnknown)));
                }
            }
            else if (projectName.Length > ProjectNameLength)
            {
                errors.Add(await ErrorAsync(FieldProject, nameof(TranslationKeys.TooLong), ProjectNameLength));
            }
        }

        // Paid in instalments: a new outflow only, at most five years, at least a cent each
        var instalments = item.Instalments ?? 1;
        var rule = item.InstalmentRule is { } chosen && Enum.IsDefined(chosen) ? chosen : InstalmentRule.FirstBusinessDay;
        if (instalments != 1)
        {
            if (entryId is not null || kind != EntryKind.Outflow)
            {
                errors.Add(new ApiValidationError(FieldInstalments, await Globalization.TranslateAsync(nameof(TranslationKeys.InstalmentsOutflowOnly))));
            }
            else if (instalments is < Instalments.Fewest or > Instalments.Most)
            {
                errors.Add(await ErrorAsync(FieldInstalments, nameof(TranslationKeys.OutOfRange), Instalments.Fewest, Instalments.Most));
            }
            else if (amount < instalments * 0.01m)
            {
                errors.Add(await ErrorAsync(FieldAmount, nameof(TranslationKeys.AmountInvalid)));
            }
            else if (Instalments.Day(day, instalments - 1, rule) > _clock.Today.AddYears(10))
            {
                errors.Add(await ErrorAsync(FieldDate, nameof(TranslationKeys.DateInvalid)));
            }
        }

        if (errors.Count > 0)
        {
            return await InvalidAsync<EntryDTO>(errors);
        }

        if (scope == EntryScope.Flips && kind != EntryKind.Transfer && project is null && projectName.Length > 0)
        {
            project = await _projectRepository.SaveAsync(userId, null, projectName, null, null, null);
        }

        if (instalments > 1)
        {
            return await SavePlanAsync();
        }

        var saved = await _entryRepository.SaveAsync(userId, entryId, new EntryRow
        {
            Kind = (byte)kind,
            Scope = (byte)scope,
            Name = name,
            Amount = amount,
            EntryDate = day.ToDateTime(TimeOnly.MinValue),
            Category = category,
            AccountUniqueID = account,
            ToAccountUniqueID = toAccount,
            ProjectUniqueID = project,
            Nights = nights,
            Channel = channel,
        });

        var row = saved is { } savedId ? await _entryRepository.GetAsync(userId, savedId) : null;
        if (row is null)
        {
            return await MissingAsync<EntryDTO>(item.Id ?? name);
        }

        var mapper = new EntryMapper(accounts, await _projectRepository.ListAsync(userId));
        return ServiceApiResponse<EntryDTO>.Success(mapper.Map(row));

        Guid? FindAccount(string? text) =>
            Guid.TryParse(text, out var accountId) && accounts.Any(candidate => candidate.AccountUniqueID == accountId) ? accountId : null;

        // One entry per instalment, all at once, sharing a plan key; answers with the first instalment.
        async Task<ServiceApiResponse<EntryDTO>> SavePlanAsync()
        {
            var plan = Guid.NewGuid();
            var schedule = Instalments.Schedule(amount, instalments, day, rule);
            var rows = schedule.Select((part, index) => new EntryRow
            {
                Kind = (byte)kind,
                Scope = (byte)scope,
                Name = name,
                Amount = part.Amount,
                EntryDate = part.Day.ToDateTime(TimeOnly.MinValue),
                Category = category,
                AccountUniqueID = account,
                ProjectUniqueID = project,
                PlanKey = plan,
                PlanPart = (short)(index + 1),
                PlanParts = (short)instalments,
            }).ToList();
            await _entryRepository.ImportAsync(userId, rows);

            var first = (await _entryRepository.ListAsync(userId, day, day)).FirstOrDefault(row => row.PlanKey == plan);
            if (first is null)
            {
                return await MissingAsync<EntryDTO>(name);
            }

            var planMapper = new EntryMapper(accounts, await _projectRepository.ListAsync(userId));
            return ServiceApiResponse<EntryDTO>.Success(planMapper.Map(first));
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(identifier, out var entryId))
        {
            return await MissingAsync<string>(identifier);
        }

        if (await _entryRepository.GetAsync(id, entryId) is null)
        {
            return await MissingAsync<string>(identifier);
        }

        await _entryRepository.DeleteAsync(id, entryId);
        return ServiceApiResponse<string>.Success(Id(entryId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<int>> DeletePlanAsync(string? userId, string? planId)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(planId, out var plan))
        {
            return await MissingAsync<int>(planId);
        }

        var deleted = await _entryRepository.DeletePlanAsync(id, plan);
        if (deleted == 0)
        {
            return await MissingAsync<int>(planId);
        }

        return ServiceApiResponse<int>.Success(deleted);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<int>> ReviewedAsync(EntryReviewedRequest item)
    {
        if (!TryUser(item.UserId, out var id))
        {
            return await MissingAsync<int>(item.UserId);
        }

        var ids = (item.Ids ?? [])
            .Select(static text => Guid.TryParse(text, out var entry) ? entry : Guid.Empty)
            .Where(static entry => entry != Guid.Empty)
            .Distinct()
            .ToList();
        if (item.Ids is { Count: > 0 } && ids.Count == 0)
        {
            return ServiceApiResponse<int>.Success(0);
        }

        return ServiceApiResponse<int>.Success(await _entryRepository.ReviewedAsync(id, ids));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<QuickAddDTO>> QuickAsync(string? userId, string? text)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<QuickAddDTO>(userId);
        }

        var (understood, amount, name, plus) = QuickAdd.Parse(text);
        if (!understood || amount > MaxAmount)
        {
            return await ProblemAsync<QuickAddDTO>(FieldText, nameof(TranslationKeys.QuickAddNoAmount));
        }

        if (name.Length > NameLength)
        {
            name = name[..NameLength];
        }

        var accounts = await _accountRepository.ListAsync(id);
        var last = name.Length == 0 ? null : await _entryRepository.SuggestAsync(id, name);

        // A plus sign always means money coming in, whatever the same words were last time.
        var kind = plus ? EntryKind.Inflow : last is null ? EntryKind.Outflow : (EntryKind)last.Kind;
        var remembered = last is not null && (EntryKind)last.Kind == kind;
        // Words never seen before: a guess from the words themselves, and a meal goes on the meal card when there is one.
        var guessed = remembered ? null : CategoryGuess.For(name, kind);
        var account = remembered && accounts.FirstOrDefault(row => row.AccountUniqueID == last!.AccountUniqueID) is { } same
            ? same
            : (!remembered && kind == EntryKind.Outflow && CategoryGuess.IsMeal(name) ? accounts.FirstOrDefault(static row => row.Kind == (byte)AccountKind.MealCard) : null)
                ?? accounts.FirstOrDefault();

        return ServiceApiResponse<QuickAddDTO>.Success(new QuickAddDTO
        {
            Understood = true,
            Amount = amount,
            Name = name,
            Kind = kind,
            Scope = remembered ? (EntryScope)last!.Scope : EntryScope.Personal,
            Category = remembered && Categories.Find(last!.Category) is { } category ? category.Key : guessed ?? Categories.Fallback(kind),
            AccountId = Id(account?.AccountUniqueID),
            AccountName = account?.Name,
            Date = _clock.Today,
            Remembered = remembered,
        });
    }
}
