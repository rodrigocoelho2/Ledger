using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Entries.Services;
using Ledger.WebApi.Features.Overview.Models.DTOs;
using Ledger.WebApi.Features.Overview.Services;
using Ledger.WebApi.Features.Projects.Repositories;
using Ledger.WebApi.Features.Rental.Models.DTOs;

namespace Ledger.WebApi.Features.Rental.Services;

/// <summary>
/// The short-term rental module.
/// </summary>
internal interface IRentalService
{
    /// <summary>A month of the rental: revenue, costs, margin, the booked nights and the year so far.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The month.</returns>
    Task<ServiceApiResponse<RentalDTO>> GetAsync(string? userId, string? month);
}

/// <summary>
/// Rental business logic.
/// </summary>
/// <param name="entryRepository">The entries.</param>
/// <param name="accountRepository">The accounts.</param>
/// <param name="projectRepository">The projects.</param>
/// <param name="clock">The clock, in the app's time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class RentalService(
    IEntryRepository entryRepository,
    IAccountRepository accountRepository,
    IProjectRepository projectRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IRentalService
{
    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IAccountRepository _accountRepository = accountRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IAppClock _clock = clock;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<RentalDTO>> GetAsync(string? userId, string? month)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<RentalDTO>(userId);
        }

        var current = Months.Start(_clock.Today);
        var start = Months.Parse(month, current);
        var end = Months.End(start);
        var yearStart = new DateOnly(start.Year, 1, 1);

        // The whole year (for "the year so far") and a few weeks before the month (a stay can start in the one before).
        var from = yearStart < start.AddDays(-RentalMath.LookBackDays) ? yearStart : start.AddDays(-RentalMath.LookBackDays);
        var entries = await _entryRepository.ListAsync(id, from, new DateOnly(start.Year, 12, 31), EntryScope.Rental);

        var ofMonth = entries.Where(entry => entry.Day >= start && entry.Day <= end).ToList();
        var (revenue, costs) = OverviewMath.Totals(ofMonth);

        var placed = entries
            .Where(RentalMath.IsBooking)
            .Select(entry => RentalMath.Place(entry, start))
            .Where(static booking => booking.Span > 0 || booking.PaidThisMonth)
            .OrderBy(static booking => booking.CheckIn)
            .ToList();
        var paid = placed.Where(static booking => booking.PaidThisMonth).ToList();
        var withNights = paid.Where(static booking => booking.Nights > 0).ToList();
        var nightsInMonth = end.Day;
        var nightsBooked = RentalMath.NightsBooked(placed, nightsInMonth);

        var ofYear = entries.Where(entry => entry.Day >= yearStart).ToList();
        var yearMonths = OverviewMath.Nets(
                Enumerable.Range(1, 12).Select(number => new DateOnly(start.Year, number, 1)), ofYear, current)
            .Where(static line => line.HasEntries)
            .ToList();

        // Each cost as a share of revenue (of the costs themselves when nothing came in).
        var split = OverviewMath.Parts(ofMonth, EntryKind.Outflow);
        foreach (var part in split)
        {
            part.Share = OverviewMath.Share(part.Amount, revenue > 0 ? revenue : costs);
        }

        var mapper = new EntryMapper(await _accountRepository.ListAsync(id), await _projectRepository.ListAsync(id));
        return ServiceApiResponse<RentalDTO>.Success(new RentalDTO
        {
            Month = Months.Text(start),
            PreviousMonth = Months.Text(start.AddMonths(-1)),
            NextMonth = Months.Text(start.AddMonths(1)),
            Revenue = revenue,
            Costs = costs,
            Margin = revenue - costs,
            MarginShare = revenue > 0 ? OverviewMath.Share(revenue - costs, revenue) : null,
            CostShare = revenue > 0 ? OverviewMath.Share(costs, revenue) : null,
            BookingCount = paid.Count,
            AverageBooking = paid.Count == 0 ? null : decimal.Round(paid.Sum(static booking => booking.Amount) / paid.Count, 2, MidpointRounding.AwayFromZero),
            NightsBooked = nightsBooked,
            NightsInMonth = nightsInMonth,
            Occupancy = OverviewMath.Share(nightsBooked, nightsInMonth),
            NightlyRate = withNights.Count == 0
                ? null
                : decimal.Round(withNights.Sum(static booking => booking.Amount) / withNights.Sum(static booking => booking.Nights), 2, MidpointRounding.AwayFromZero),
            Bookings = placed,
            Split = split,
            Year = start.Year,
            YearMargin = yearMonths.Sum(static line => line.Net),
            YearMonths = yearMonths,
            Entries = mapper.Map(ofMonth),
        });
    }
}
