namespace Ledger.WebApi.Features.Accounts.Models.Entities;

/// <summary>
/// An account (dbo.Account) with its balance worked out (dbo.AccountList).
/// </summary>
public sealed class AccountRow
{
    /// <summary>Gets or sets the public identifier.</summary>
    public Guid AccountUniqueID { get; set; }

    /// <summary>Gets or sets the name the person gave it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind (<see cref="Ledger.Library.Enums.AccountKind"/>).</summary>
    public byte Kind { get; set; }

    /// <summary>Gets or sets what was in it before the first entry.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Gets or sets its place in the list.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the balance: the opening balance plus every entry that touched it.</summary>
    public decimal Balance { get; set; }
}
