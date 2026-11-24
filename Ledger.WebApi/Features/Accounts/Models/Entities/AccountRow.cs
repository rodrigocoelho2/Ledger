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

    /// <summary>Gets or sets what the entries added up to (the balance without the opening balance), up to today.</summary>
    public decimal Movement { get; set; }

    /// <summary>Gets or sets where the value comes from when it isn't the entries (1: Obol).</summary>
    public byte? ValueSource { get; set; }

    /// <summary>Gets or sets the user name at the source (empty: the same as in Ledger).</summary>
    public string? ValueRef { get; set; }

    /// <summary>Gets or sets the last value fetched from the source.</summary>
    public decimal? Value { get; set; }

    /// <summary>Gets or sets the day of that value.</summary>
    public DateTime? ValueDate { get; set; }

    /// <summary>Gets or sets the bank the account syncs from (null: none).</summary>
    public string? BankName { get; set; }

    /// <summary>Gets or sets when that bank last synced (UTC).</summary>
    public DateTime? BankSyncedAt { get; set; }

    /// <summary>Gets or sets the balance the bank reported on the last sync.</summary>
    public decimal? BankBalance { get; set; }

    /// <summary>The balance shown: the source's last value for an account valued elsewhere, else the opening balance plus the entries.</summary>
    /// <returns>The row, with its balance set.</returns>
    public AccountRow Settle()
    {
        Balance = ValueSource is not null && Value is { } value ? value : OpeningBalance + Movement;
        return this;
    }
}

/// <summary>
/// What an account valued elsewhere was worth on a day (dbo.AccountValue).
/// </summary>
public sealed class AccountValueRow
{
    /// <summary>Gets or sets the account.</summary>
    public Guid AccountUniqueID { get; set; }

    /// <summary>Gets or sets the day.</summary>
    public DateTime ValueDate { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public decimal Value { get; set; }
}

/// <summary>
/// An account valued elsewhere, with its owner: what the background refresh goes through (dbo.AccountValuedAll).
/// </summary>
public sealed class ValuedAccountRow
{
    /// <summary>Gets or sets the owner.</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets the owner's user name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the account.</summary>
    public Guid AccountUniqueID { get; set; }

    /// <summary>Gets or sets where its value comes from.</summary>
    public byte ValueSource { get; set; }

    /// <summary>Gets or sets the user name at the source (empty: the same as in Ledger).</summary>
    public string? ValueRef { get; set; }
}
