using Ledger.Library.Enums;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Projects.Models.Entities;

namespace Ledger.WebApi.Features.Entries.Services;

/// <summary>
/// Turns entry rows into what the Website sees, with the names of the accounts and projects they point at.
/// </summary>
/// <param name="accounts">The user's accounts.</param>
/// <param name="projects">The user's projects.</param>
internal sealed class EntryMapper(IEnumerable<AccountRow> accounts, IEnumerable<ProjectRow> projects)
{
    private readonly Dictionary<Guid, string> _accounts = accounts.ToDictionary(static account => account.AccountUniqueID, static account => account.Name);
    private readonly Dictionary<Guid, string> _projects = projects.ToDictionary(static project => project.ProjectUniqueID, static project => project.Name);

    /// <summary>Maps one row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The entry.</returns>
    public EntryDTO Map(EntryRow row) => new()
    {
        Id = row.EntryUniqueID.ToString("N"),
        Kind = (EntryKind)row.Kind,
        Scope = (EntryScope)row.Scope,
        Name = row.Name,
        Amount = row.Amount,
        Date = row.Day,
        Category = row.Category,
        AccountId = row.AccountUniqueID?.ToString("N"),
        AccountName = row.AccountUniqueID is { } account && _accounts.TryGetValue(account, out var accountName) ? accountName : null,
        ToAccountId = row.ToAccountUniqueID?.ToString("N"),
        ToAccountName = row.ToAccountUniqueID is { } to && _accounts.TryGetValue(to, out var toName) ? toName : null,
        ProjectId = row.ProjectUniqueID?.ToString("N"),
        ProjectName = row.ProjectUniqueID is { } project && _projects.TryGetValue(project, out var projectName) ? projectName : null,
        Nights = row.Nights,
        Channel = row.Channel,
        PlanId = row.PlanKey?.ToString("N"),
        PlanPart = row.PlanPart,
        PlanParts = row.PlanParts,
        FromBank = row.ExternalID is not null,
        Review = row.Review == true,
    };

    /// <summary>Maps many rows, keeping their order.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The entries.</returns>
    public List<EntryDTO> Map(IEnumerable<EntryRow> rows) => rows.Select(Map).ToList();
}

/// <summary>
/// Months as the API writes them (yyyy-MM).
/// </summary>
internal static class Months
{
    /// <summary>Reads a month; anything else is the fallback.</summary>
    /// <param name="text">"2026-10".</param>
    /// <param name="fallback">The month to use otherwise (any day of it).</param>
    /// <returns>The first day of the month.</returns>
    public static DateOnly Parse(string? text, DateOnly fallback)
    {
        if (text is { Length: 7 } && text[4] == '-'
            && int.TryParse(text.AsSpan(0, 4), out var year) && int.TryParse(text.AsSpan(5, 2), out var month)
            && year is >= 1990 and <= 2200 && month is >= 1 and <= 12)
        {
            return new DateOnly(year, month, 1);
        }

        return Start(fallback);
    }

    /// <summary>The first day of a day's month.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The first day.</returns>
    public static DateOnly Start(DateOnly day) => new(day.Year, day.Month, 1);

    /// <summary>The last day of a month.</summary>
    /// <param name="monthStart">Its first day.</param>
    /// <returns>The last day.</returns>
    public static DateOnly End(DateOnly monthStart) => monthStart.AddMonths(1).AddDays(-1);

    /// <summary>Writes a month.</summary>
    /// <param name="monthStart">Any day of it.</param>
    /// <returns>"2026-10".</returns>
    public static string Text(DateOnly monthStart) => monthStart.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
