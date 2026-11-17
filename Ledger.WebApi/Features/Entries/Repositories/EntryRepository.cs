using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Features.Entries.Models.Entities;

namespace Ledger.WebApi.Features.Entries.Repositories;

/// <summary>
/// The entries of a user (dbo.Entry).
/// </summary>
internal interface IEntryRepository
{
    /// <summary>Lists entries, newest first. Every filter is optional.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="from">The first day (included).</param>
    /// <param name="to">The last day (included).</param>
    /// <param name="scope">The part of life.</param>
    /// <returns>The entries.</returns>
    Task<List<EntryRow>> ListAsync(Guid userId, DateOnly? from = null, DateOnly? to = null, EntryScope? scope = null);

    /// <summary>Gets one entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="entryId">The entry.</param>
    /// <returns>The entry, or null.</returns>
    Task<EntryRow?> GetAsync(Guid userId, Guid entryId);

    /// <summary>The last inflow or outflow written with the same words (or starting with them).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="name">The words.</param>
    /// <returns>The entry, or null.</returns>
    Task<EntryRow?> SuggestAsync(Guid userId, string name);

    /// <summary>Adds an entry (no id) or changes one.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="entryId">The entry to change, or null for a new one.</param>
    /// <param name="row">Its values.</param>
    /// <returns>The entry's id, or null when the entry to change isn't this user's.</returns>
    Task<Guid?> SaveAsync(Guid userId, Guid? entryId, EntryRow row);

    /// <summary>Deletes one entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="entryId">The entry.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId, Guid entryId);

    /// <summary>Adds many entries at once.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="rows">The entries.</param>
    /// <returns>How many were added.</returns>
    Task<int> ImportAsync(Guid userId, IReadOnlyList<EntryRow> rows);

    /// <summary>Start over: deletes every entry, project, account and spending cap of the user.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>A task.</returns>
    Task ResetAsync(Guid userId);
}

/// <summary>
/// The entries in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Entries).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class EntryRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IEntryRepository
{
    /// <inheritdoc />
    public Task<List<EntryRow>> ListAsync(Guid userId, DateOnly? from = null, DateOnly? to = null, EntryScope? scope = null) =>
        QueryAsync<EntryRow>("[dbo].[EntryList]", new
        {
            UserUniqueID = userId,
            From = from is { } first ? Date(first) : (DateTime?)null,
            To = to is { } last ? Date(last) : (DateTime?)null,
            Scope = scope is { } part ? (byte)part : (byte?)null,
        });

    /// <inheritdoc />
    public Task<EntryRow?> GetAsync(Guid userId, Guid entryId) =>
        QuerySingleOrDefaultAsync<EntryRow>("[dbo].[EntryGet]", new { UserUniqueID = userId, EntryUniqueID = entryId });

    /// <inheritdoc />
    public Task<EntryRow?> SuggestAsync(Guid userId, string name) =>
        QuerySingleOrDefaultAsync<EntryRow>("[dbo].[EntrySuggest]", new { UserUniqueID = userId, Name = name, Like = EntryNames.StartsWithPattern(name) });

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? entryId, EntryRow row) =>
        ExecuteScalarAsync<Guid?>("[dbo].[EntrySave]", new
        {
            UserUniqueID = userId,
            EntryUniqueID = entryId,
            row.Kind,
            row.Scope,
            row.Name,
            row.Amount,
            row.EntryDate,
            row.Category,
            row.AccountUniqueID,
            row.ToAccountUniqueID,
            row.ProjectUniqueID,
            row.Nights,
            row.Channel,
        });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid entryId) =>
        ExecuteAsync("[dbo].[EntryDelete]", new { UserUniqueID = userId, EntryUniqueID = entryId });

    /// <inheritdoc />
    public async Task<int> ImportAsync(Guid userId, IReadOnlyList<EntryRow> rows) =>
        rows.Count == 0 ? 0 : await ExecuteScalarAsync<int>("[dbo].[EntryImport]", new { UserUniqueID = userId, Rows = Json(rows) });

    /// <inheritdoc />
    public Task ResetAsync(Guid userId) => ExecuteAsync("[dbo].[LedgerReset]", new { UserUniqueID = userId });
}

/// <summary>
/// The entries in memory (demo mode).
/// </summary>
/// <param name="store">The memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryEntryRepository(MemoryStore store, TimeProvider timeProvider) : IEntryRepository
{
    /// <inheritdoc />
    public Task<List<EntryRow>> ListAsync(Guid userId, DateOnly? from = null, DateOnly? to = null, EntryScope? scope = null)
    {
        lock (store.Gate)
        {
            var entries = store.For(userId).Entries;
            var rows = entries
                .Select(static (entry, index) => (entry, index))
                .Where(item => (from is null || item.entry.Day >= from)
                    && (to is null || item.entry.Day <= to)
                    && (scope is null || item.entry.Scope == (byte)scope))
                .OrderByDescending(static item => item.entry.EntryDate)
                .ThenByDescending(static item => item.index)
                .Select(static item => Copy(item.entry))
                .ToList();
            return Task.FromResult(rows);
        }
    }

    /// <inheritdoc />
    public Task<EntryRow?> GetAsync(Guid userId, Guid entryId)
    {
        lock (store.Gate)
        {
            var entry = store.For(userId).Entries.FirstOrDefault(entry => entry.EntryUniqueID == entryId);
            return Task.FromResult(entry is null ? null : Copy(entry));
        }
    }

    /// <inheritdoc />
    public Task<EntryRow?> SuggestAsync(Guid userId, string name)
    {
        lock (store.Gate)
        {
            var entries = store.For(userId).Entries;
            var match = entries
                .Select(static (entry, index) => (entry, index))
                .Where(item => item.entry.Kind != (byte)EntryKind.Transfer
                    && item.entry.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(static item => item.entry.EntryDate)
                .ThenByDescending(static item => item.index)
                .Select(static item => item.entry)
                .FirstOrDefault();
            return Task.FromResult(match is null ? null : Copy(match));
        }
    }

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? entryId, EntryRow row)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            var copy = Copy(row);

            // Like dbo.EntrySave: what isn't this user's is left empty.
            copy.AccountUniqueID = ledger.Accounts.Any(account => account.AccountUniqueID == row.AccountUniqueID) ? row.AccountUniqueID : null;
            copy.ToAccountUniqueID = ledger.Accounts.Any(account => account.AccountUniqueID == row.ToAccountUniqueID) ? row.ToAccountUniqueID : null;
            copy.ProjectUniqueID = ledger.Projects.Any(project => project.ProjectUniqueID == row.ProjectUniqueID) ? row.ProjectUniqueID : null;

            if (entryId is null)
            {
                copy.EntryUniqueID = Guid.NewGuid();
                copy.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
                ledger.Entries.Add(copy);
                return Task.FromResult<Guid?>(copy.EntryUniqueID);
            }

            var index = ledger.Entries.FindIndex(entry => entry.EntryUniqueID == entryId);
            if (index < 0)
            {
                return Task.FromResult<Guid?>(null);
            }

            copy.EntryUniqueID = entryId.Value;
            copy.CreatedAt = ledger.Entries[index].CreatedAt;
            ledger.Entries[index] = copy;
            return Task.FromResult<Guid?>(copy.EntryUniqueID);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid entryId)
    {
        lock (store.Gate)
        {
            store.For(userId).Entries.RemoveAll(entry => entry.EntryUniqueID == entryId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> ImportAsync(Guid userId, IReadOnlyList<EntryRow> rows)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var added = 0;
            foreach (var row in rows)
            {
                if (row.Amount <= 0 || string.IsNullOrEmpty(row.Name))
                {
                    continue;
                }

                var copy = Copy(row);
                copy.EntryUniqueID = Guid.NewGuid();
                copy.CreatedAt = now;
                copy.AccountUniqueID = ledger.Accounts.Any(account => account.AccountUniqueID == row.AccountUniqueID) ? row.AccountUniqueID : null;
                copy.ToAccountUniqueID = ledger.Accounts.Any(account => account.AccountUniqueID == row.ToAccountUniqueID) ? row.ToAccountUniqueID : null;
                copy.ProjectUniqueID = ledger.Projects.Any(project => project.ProjectUniqueID == row.ProjectUniqueID) ? row.ProjectUniqueID : null;
                ledger.Entries.Add(copy);
                added++;
            }

            return Task.FromResult(added);
        }
    }

    /// <inheritdoc />
    public Task ResetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            store.Ledgers.Remove(userId);
        }

        return Task.CompletedTask;
    }

    private static EntryRow Copy(EntryRow entry) => new()
    {
        EntryUniqueID = entry.EntryUniqueID,
        Kind = entry.Kind,
        Scope = entry.Scope,
        Name = entry.Name,
        Amount = entry.Amount,
        EntryDate = entry.EntryDate.Date,
        Category = entry.Category,
        AccountUniqueID = entry.AccountUniqueID,
        ToAccountUniqueID = entry.ToAccountUniqueID,
        ProjectUniqueID = entry.ProjectUniqueID,
        Nights = entry.Nights,
        Channel = entry.Channel,
        CreatedAt = entry.CreatedAt,
    };
}

/// <summary>
/// Small rules about the words of an entry.
/// </summary>
internal static class EntryNames
{
    /// <summary>
    /// The LIKE pattern for "starts with these words", with the wildcards of the words themselves escaped (ESCAPE '\').
    /// </summary>
    /// <param name="name">The words.</param>
    /// <returns>The pattern.</returns>
    public static string StartsWithPattern(string name) =>
        name.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal) + "%";
}
