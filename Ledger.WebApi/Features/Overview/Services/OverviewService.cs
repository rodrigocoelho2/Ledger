using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Accounts.Services;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Models.DTOs;
using Ledger.WebApi.Features.Overview.Repositories;
using Ledger.WebApi.Features.Projects.Repositories;

namespace Ledger.WebApi.Features.Overview.Services;

/// <summary>
/// The overview of a month.
/// </summary>
internal interface IOverviewService
{
    /// <summary>The overview of a month of the person's own money.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The overview.</returns>
    Task<ServiceApiResponse<OverviewDTO>> GetAsync(string? userId, string? month);

    /// <summary>Sets the spending cap of a month, or removes it.</summary>
    /// <param name="item">The month and the cap.</param>
    /// <returns>The month.</returns>
    Task<ServiceApiResponse<string>> SaveCapAsync(CapSaveRequest item);
}

/// <summary>
/// Overview business logic: reads the entries around a month once and adds them up.
/// </summary>
/// <param name="entryRepository">The entries.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="projectRepository">The projects.</param>
/// <param name="capRepository">The spending caps.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class OverviewService(
    IEntryRepository entryRepository,
    IAccountRepository accountRepository,
    IProjectRepository projectRepository,
    ICapRepository capRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IOverviewService
{
    /// <summary>How many entries the overview lists.</summary>
    public const int LatestEntries = 9;

    private const string FieldAmount = "Amount";

    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly ICapRepository _capRepository = capRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<OverviewDTO>> GetAsync(string? userId, string? month)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<OverviewDTO>(userId);
        }

        var current = Months.Start(_clock.Today);
        var start = Months.Parse(month, current);
        var end = Months.End(start);
        var previous = start.AddMonths(-1);
        var strip = OverviewMath.Strip(start, current);

        // One read covers the strip, the month before and the month itself; every scope, because an account's month
        // is moved by rental and project entries too.
        var first = strip[0] < previous ? strip[0] : previous;
        var last = Months.End(strip[^1] > start ? strip[^1] : start);
        var everything = await _entryRepository.ListAsync(id, first, last);
        var personal = everything.Where(static entry => entry.Scope == (byte)EntryScope.Personal).ToList();

        var ofMonth = personal.Where(entry => entry.Day >= start && entry.Day <= end).ToList();
        var (incoming, outgoing) = OverviewMath.Totals(ofMonth);
        var ofPrevious = personal.Where(entry => entry.Day >= previous && entry.Day < start && entry.Kind != (byte)EntryKind.Transfer).ToList();
        var (previousIn, previousOut) = OverviewMath.Totals(ofPrevious);

        var (oneOffIn, oneOffOut) = OverviewMath.OneOffs(ofMonth);

        // An account's change this month counts only what has happened: instalments still to come wait for their day.
        var today = _clock.Today;
        var accounts = await _accountRepository.ListAsync(id);
        var monthOfEveryScope = everything.Where(entry => entry.Day >= start && entry.Day <= end && entry.Day <= today).ToList();
        var ahead = await _entryRepository.ListAsync(id, today.AddDays(1), null);
        var plans = OverviewMath.Plans(ahead, accounts.ToDictionary(static account => account.AccountUniqueID, static account => account.Name));
        var values = accounts.Any(static account => account.ValueSource is not null) ? await _accountRepository.ValuesAsync(id, start) : [];
        var caps = await _capRepository.ListAsync(id);
        decimal? cap = caps.TryGetValue(start, out var amount) ? amount : null;
        var mapper = new EntryMapper(accounts, await _projectRepository.ListAsync(id));

        return ServiceApiResponse<OverviewDTO>.Success(new OverviewDTO
        {
            Month = Months.Text(start),
            PreviousMonth = Months.Text(previous),
            NextMonth = Months.Text(start.AddMonths(1)),
            IsCurrentMonth = start == current,
            In = incoming,
            Out = outgoing,
            Net = incoming - outgoing,
            SavingsRate = OverviewMath.SavingsRate(incoming - oneOffIn, outgoing - oneOffOut),
            PreviousNet = ofPrevious.Count == 0 ? null : previousIn - previousOut,
            NetWorth = accounts.Sum(static account => account.Balance),
            Cap = cap,
            CapUsed = cap is { } limit && limit > 0 ? decimal.Round(outgoing / limit, 4, MidpointRounding.AwayFromZero) : null,
            Months = OverviewMath.Nets(strip, personal, current),
            Sources = OverviewMath.Parts(ofMonth, EntryKind.Inflow),
            Targets = OverviewMath.Parts(ofMonth, EntryKind.Outflow),
            Accounts = accounts.Select(account =>
            {
                var dto = AccountService.ToDTO(account);
                dto.MonthChange = account.ValueSource is null
                    ? Balances.Movement(account.AccountUniqueID, monthOfEveryScope)
                    : OverviewMath.ValueChange(values.Where(value => value.AccountUniqueID == account.AccountUniqueID), start, end);
                return dto;
            }).ToList(),
            Entries = mapper.Map(ofMonth.Take(LatestEntries)),
            EntryCount = ofMonth.Count,
            OneOffIn = oneOffIn,
            OneOffOut = oneOffOut,
            Plans = plans,
            PlansLeft = plans.Sum(static plan => plan.LeftAmount),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> SaveCapAsync(CapSaveRequest item)
    {
        if (!TryUser(item.UserId, out var id))
        {
            return await MissingAsync<string>(item.UserId);
        }

        var start = Months.Parse(item.Month, _clock.Today);
        var amount = item.Amount is { } value ? decimal.Round(value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        if (amount is < 0m or > MaxAmount)
        {
            return await InvalidAsync<string>([await ErrorAsync(FieldAmount, nameof(TranslationKeys.OutOfRange), 0, MaxAmount)]);
        }

        await _capRepository.SaveAsync(id, start, amount is null or 0m ? null : amount);
        return ServiceApiResponse<string>.Success(Months.Text(start));
    }
}
