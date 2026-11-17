using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.Entities;

namespace Ledger.WebApi.Features.Accounts.Services;

/// <summary>
/// How entries move an account's balance. The same rule as dbo.AccountList: inflows add, outflows and transfers out
/// take away, transfers in add.
/// </summary>
internal static class Balances
{
    /// <summary>What a set of entries added to (or took from) an account.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="entries">The entries (any accounts; the others are ignored).</param>
    /// <returns>The change, positive or negative.</returns>
    public static decimal Movement(Guid accountId, IEnumerable<EntryRow> entries)
    {
        var total = 0m;
        foreach (var entry in entries)
        {
            if (entry.AccountUniqueID == accountId)
            {
                total += entry.Kind == (byte)EntryKind.Inflow ? entry.Amount : -entry.Amount;
            }

            if (entry.ToAccountUniqueID == accountId && entry.Kind == (byte)EntryKind.Transfer)
            {
                total += entry.Amount;
            }
        }

        return total;
    }
}
