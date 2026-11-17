using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Projects.Models.Entities;
using Ledger.WebApi.Features.Users.Models.Entities;

namespace Ledger.WebApi.Extensions.Data;

/// <summary>
/// The tables of demo mode (<see cref="Features.LedgerOptions.DemoMode"/>): the same shapes as the SQL Server tables,
/// kept in memory and lost on restart. The Memory* repositories read and write it under <see cref="Gate"/>.
/// </summary>
internal sealed class MemoryStore
{
    /// <summary>Gets the lock every memory repository takes.</summary>
    public Lock Gate { get; } = new();

    /// <summary>Gets the users.</summary>
    public List<UserAccount> Users { get; } = [];

    /// <summary>Gets the ledgers, by user.</summary>
    public Dictionary<Guid, MemoryLedger> Ledgers { get; } = [];

    /// <summary>
    /// Gets (creating it) the ledger of a user. Call it under <see cref="Gate"/>.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The ledger.</returns>
    public MemoryLedger For(Guid userId)
    {
        if (!Ledgers.TryGetValue(userId, out var ledger))
        {
            ledger = new MemoryLedger();
            Ledgers[userId] = ledger;
        }

        return ledger;
    }
}

/// <summary>
/// Everything one user owns in demo mode.
/// </summary>
internal sealed class MemoryLedger
{
    /// <summary>Gets the accounts (the Balance of these rows is never kept: it is worked out when they are read).</summary>
    public List<AccountRow> Accounts { get; } = [];

    /// <summary>Gets the entries, in the order they were written.</summary>
    public List<EntryRow> Entries { get; } = [];

    /// <summary>Gets the buy-and-sell projects.</summary>
    public List<ProjectRow> Projects { get; } = [];

    /// <summary>Gets the spending caps, by the first day of the month.</summary>
    public Dictionary<DateOnly, decimal> Caps { get; } = [];
}
