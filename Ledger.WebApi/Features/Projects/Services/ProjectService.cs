using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Services;
using Ledger.WebApi.Features.Projects.Models.DTOs;
using Ledger.WebApi.Features.Projects.Repositories;

namespace Ledger.WebApi.Features.Projects.Services;

/// <summary>
/// The buy-and-sell module.
/// </summary>
internal interface IProjectService
{
    /// <summary>Every project, worked out from its entries, and what they add up to.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The projects.</returns>
    Task<ServiceApiResponse<ProjectsDTO>> ListAsync(string? userId);

    /// <summary>Adds or changes a project.</summary>
    /// <param name="item">The project.</param>
    /// <returns>The project's id, or validation problems.</returns>
    Task<ServiceApiResponse<string>> SaveAsync(ProjectSaveRequest item);

    /// <summary>Deletes a project (its entries stay, without a project).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The project.</param>
    /// <returns>The deleted id.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier);
}

/// <summary>
/// Project business logic.
/// </summary>
/// <param name="projectRepository">The projects.</param>
/// <param name="entryRepository">The entries.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class ProjectService(
    IProjectRepository projectRepository,
    IEntryRepository entryRepository,
    IAccountRepository accountRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IProjectService
{
    private const string FieldName = "Name";
    private const string FieldTargetSale = "TargetSale";
    private const string FieldWorksBudget = "WorksBudget";

    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ProjectsDTO>> ListAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<ProjectsDTO>(userId);
        }

        var projects = await _projectRepository.ListAsync(id);
        var entries = await _entryRepository.ListAsync(id, scope: EntryScope.Flips);
        var mapper = new EntryMapper(await _accountRepository.ListAsync(id), projects);
        var today = _clock.Today;

        var built = projects.Select(project =>
        {
            var own = entries.Where(entry => entry.ProjectUniqueID == project.ProjectUniqueID).ToList();
            var dto = ProjectMath.Build(project, own, today);
            dto.Entries = mapper.Map(own);
            return dto;
        }).ToList();

        var sold = built.Where(static project => project.State == ProjectState.Sold).ToList();
        var open = built.Where(static project => project.State == ProjectState.InProgress).ToList();
        var investedInSold = sold.Sum(static project => project.Invested);
        var profit = sold.Sum(static project => project.Result ?? 0m);

        return ServiceApiResponse<ProjectsDTO>.Success(new ProjectsDTO
        {
            RealisedProfit = profit,
            SoldCount = sold.Count,
            InvestedInSold = investedInSold,
            ReturnOnSold = sold.Count > 0 && investedInSold > 0 ? OverviewMath.Share(profit, investedInSold) : null,
            CapitalAtWork = open.Sum(static project => project.Invested),
            OpenCount = open.Count,
            AverageDays = sold.Count == 0 ? null : (int)Math.Round(sold.Average(static project => project.Days ?? 0), MidpointRounding.AwayFromZero),
            Projects =
            [
                .. open.OrderByDescending(static project => project.FirstDay),
                .. sold.OrderByDescending(static project => project.SaleDay),
            ],
            Loose = mapper.Map(entries.Where(static entry => entry.ProjectUniqueID is null)),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> SaveAsync(ProjectSaveRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<string>(item.UserId);
        }

        Guid? projectId = null;
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (!Guid.TryParse(item.Id, out var parsed))
            {
                return await MissingAsync<string>(item.Id);
            }

            projectId = parsed;
        }

        var name = (item.Name ?? string.Empty).Trim();
        var targetSale = Money(item.TargetSale);
        var worksBudget = Money(item.WorksBudget);

        var errors = new List<ApiValidationError>();
        var projects = await _projectRepository.ListAsync(userId);
        if (name.Length == 0)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }
        else if (name.Length > EntryService.ProjectNameLength)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.TooLong), EntryService.ProjectNameLength));
        }
        else if (projects.Any(project => project.ProjectUniqueID != projectId && project.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ApiValidationError(FieldName, await Globalization.TranslateAsync(nameof(TranslationKeys.AlreadyExists), name)));
        }

        if (targetSale is < 0m or > MaxAmount)
        {
            errors.Add(await ErrorAsync(FieldTargetSale, nameof(TranslationKeys.OutOfRange), 0, MaxAmount));
        }

        if (worksBudget is < 0m or > MaxAmount)
        {
            errors.Add(await ErrorAsync(FieldWorksBudget, nameof(TranslationKeys.OutOfRange), 0, MaxAmount));
        }

        if (errors.Count > 0)
        {
            return await InvalidAsync<string>(errors);
        }

        var saved = await _projectRepository.SaveAsync(userId, projectId, name, targetSale is 0m ? null : targetSale, worksBudget is 0m ? null : worksBudget);
        return saved is { } id ? ServiceApiResponse<string>.Success(Id(id)) : await MissingAsync<string>(item.Id ?? name);

        static decimal? Money(decimal? value) => value is { } amount ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(identifier, out var projectId))
        {
            return await MissingAsync<string>(identifier);
        }

        if ((await _projectRepository.ListAsync(id)).All(project => project.ProjectUniqueID != projectId))
        {
            return await MissingAsync<string>(identifier);
        }

        await _projectRepository.DeleteAsync(id, projectId);
        return ServiceApiResponse<string>.Success(Id(projectId));
    }
}
