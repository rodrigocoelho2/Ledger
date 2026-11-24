using Ledger.Library.Enums;

namespace Ledger.WebApi.Features.Accounts.Models.DTOs;

/// <summary>An account with its balance.</summary>
public sealed class AccountDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind.</summary>
    public AccountKind Kind { get; set; }

    /// <summary>Gets or sets what was in it before the first entry.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Gets or sets the balance today.</summary>
    public decimal Balance { get; set; }

    /// <summary>Gets or sets what the month being looked at added or took (only on the overview).</summary>
    public decimal? MonthChange { get; set; }

    /// <summary>Gets or sets where the balance comes from (null: the entries).</summary>
    public ValueSource? ValueSource { get; set; }

    /// <summary>Gets or sets the user name at that source (null: the same as in Ledger).</summary>
    public string? ValueRef { get; set; }

    /// <summary>Gets or sets the day of the last value kept.</summary>
    public DateOnly? ValueDate { get; set; }

    /// <summary>Gets or sets what went wrong the last time the value was asked for.</summary>
    public string? ValueError { get; set; }

    /// <summary>Gets or sets the bank whose account goes into this one.</summary>
    public string? BankName { get; set; }

    /// <summary>Gets or sets when the bank was last synced.</summary>
    public DateTimeOffset? BankSyncedAt { get; set; }

    /// <summary>Gets or sets what Ledger has more than the bank (null: no bank, or not synced yet).</summary>
    public decimal? BankDifference { get; set; }
}

/// <summary>Adds an account (no <see cref="Id"/>) or changes one.</summary>
public sealed class AccountSaveRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the account to change (empty: a new one).</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public AccountKind? Kind { get; set; }

    /// <summary>Gets or sets what was in it before the first entry.</summary>
    public decimal? OpeningBalance { get; set; }

    /// <summary>Gets or sets where the balance comes from (null: the entries).</summary>
    public ValueSource? ValueSource { get; set; }

    /// <summary>Gets or sets the user name at that source (empty: the same as in Ledger).</summary>
    public string? ValueRef { get; set; }
}

/// <summary>The order the accounts were dragged into.</summary>
public sealed class AccountOrderRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the account ids, first to last.</summary>
    public List<string>? Order { get; set; }
}
