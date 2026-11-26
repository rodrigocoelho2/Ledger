using Ledger.Library.Calendar;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Repositories;
using Ledger.WebApi.Features.Users.Repositories;

namespace Ledger.WebApi.Features.Entries.Services;

/// <summary>
/// Where a person's months begin: the calendar, or their payday (Settings).
/// </summary>
internal interface IMonthService
{
    /// <summary>A person's months around a stretch of days.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="from">The first day of interest.</param>
    /// <param name="to">The last day of interest.</param>
    /// <returns>The months, with the salaries the bank brought in near the stretch.</returns>
    Task<MonthPlan> PlanAsync(Guid userId, DateOnly from, DateOnly to);
}

/// <summary>
/// Reads the payday from Settings and, when a word of the salary's description is set, the salaries themselves: personal
/// inflows whose description holds it.
/// </summary>
/// <param name="userRepository">The users (Settings come with them).</param>
/// <param name="entryRepository">The entries.</param>
internal sealed class MonthService(IUserRepository userRepository, IEntryRepository entryRepository) : IMonthService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IEntryRepository _entryRepository = entryRepository;

    /// <inheritdoc />
    public async Task<MonthPlan> PlanAsync(Guid userId, DateOnly from, DateOnly to)
    {
        var user = await _userRepository.GetAsync(userId);
        if (user is null || user.PayDay is not { } payDay || payDay < 2)
        {
            return MonthPlan.Calendar;
        }

        var payer = user.Payer?.Trim();
        if (string.IsNullOrEmpty(payer))
        {
            return new MonthPlan(payDay);
        }

        // A month or two either side, so the months at both ends of the stretch find their salary too.
        var rows = await _entryRepository.ListAsync(userId, from.AddMonths(-2), to.AddMonths(2), EntryScope.Personal);
        var salaries = rows
            .Where(row => row.Kind == (byte)EntryKind.Inflow && row.Name.Contains(payer, StringComparison.OrdinalIgnoreCase))
            .Select(static row => row.Day);
        return new MonthPlan(payDay, salaries);
    }
}
