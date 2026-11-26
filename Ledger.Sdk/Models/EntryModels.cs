using Ledger.Library.Enums;

namespace Ledger.Sdk.Models;

/// <summary>One line of the ledger.</summary>
public sealed class EntryDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets which way the money moved.</summary>
    public EntryKind Kind { get; set; }

    /// <summary>Gets or sets the part of life it belongs to.</summary>
    public EntryScope Scope { get; set; }

    /// <summary>Gets or sets what it was.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount (always positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the day (for a booking, the check-in).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the category key (null on transfers).</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the account it left or came into (null: the account was deleted).</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets that account's name.</summary>
    public string? AccountName { get; set; }

    /// <summary>Gets or sets the account a transfer went to.</summary>
    public string? ToAccountId { get; set; }

    /// <summary>Gets or sets that account's name.</summary>
    public string? ToAccountName { get; set; }

    /// <summary>Gets or sets the buy-and-sell project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets the project's name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Gets or sets the nights of a rental booking.</summary>
    public int? Nights { get; set; }

    /// <summary>Gets or sets where a rental booking came from.</summary>
    public string? Channel { get; set; }

    /// <summary>Gets or sets the instalment plan it belongs to (null: none).</summary>
    public string? PlanId { get; set; }

    /// <summary>Gets or sets which instalment it is (1, 2, 3…).</summary>
    public int? PlanPart { get; set; }

    /// <summary>Gets or sets how many instalments the plan has.</summary>
    public int? PlanParts { get; set; }

    /// <summary>Gets or sets a value indicating whether it came from the bank.</summary>
    public bool FromBank { get; set; }

    /// <summary>Gets or sets a value indicating whether it came from the bank and nobody has looked at it yet.</summary>
    public bool Review { get; set; }
}

/// <summary>Adds an entry (no <see cref="Id"/>) or changes one.</summary>
public sealed class EntrySaveRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the entry to change (empty: a new one).</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets which way the money moved.</summary>
    public EntryKind? Kind { get; set; }

    /// <summary>Gets or sets the part of life (empty: personal).</summary>
    public EntryScope? Scope { get; set; }

    /// <summary>Gets or sets what it was.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Gets or sets the day.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Gets or sets the category key (not on transfers).</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets the account a transfer goes to.</summary>
    public string? ToAccountId { get; set; }

    /// <summary>Gets or sets the buy-and-sell project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets a project by name instead: it is created when the person doesn't have it yet.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Gets or sets the nights of a rental booking.</summary>
    public int? Nights { get; set; }

    /// <summary>Gets or sets where a rental booking came from.</summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Gets or sets in how many monthly instalments a new outflow is paid (empty or 1: at once). The amount is then the
    /// total, the date is the day of the first instalment, and one entry is written for each instalment.
    /// </summary>
    public int? Instalments { get; set; }

    /// <summary>Gets or sets when the instalments after the first are paid (empty: the first business day of each month).</summary>
    public InstalmentRule? InstalmentRule { get; set; }
}

/// <summary>The entries of one month.</summary>
public sealed class EntryMonthDTO
{
    /// <summary>Gets or sets the month (yyyy-MM).</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Gets or sets the first day of the month (the payday when months follow the salary).</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the last day of the month.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets the entries that pass the filters, newest first.</summary>
    public List<EntryDTO> Entries { get; set; } = [];

    /// <summary>Gets or sets how many entries the month has before the category filter.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the inflows of the listed entries.</summary>
    public decimal In { get; set; }

    /// <summary>Gets or sets the outflows of the listed entries.</summary>
    public decimal Out { get; set; }

    /// <summary>Gets or sets the category keys the month uses (for the filter), most used first.</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the month has transfers.</summary>
    public bool HasTransfers { get; set; }

    /// <summary>Gets or sets how many entries of the month came from the bank and wait to be looked at.</summary>
    public int ReviewCount { get; set; }
}

/// <summary>Marks entries brought from the bank as looked at.</summary>
public sealed class EntryReviewedRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the entries (empty: every one of the person).</summary>
    public List<string>? Ids { get; set; }
}

/// <summary>What quick add understood from a line such as "9.80 lunch".</summary>
public sealed class QuickAddDTO
{
    /// <summary>Gets or sets a value indicating whether an amount was found.</summary>
    public bool Understood { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the words.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets which way the money moves.</summary>
    public EntryKind Kind { get; set; }

    /// <summary>Gets or sets the part of life.</summary>
    public EntryScope Scope { get; set; }

    /// <summary>Gets or sets the category key.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the account.</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets the account's name.</summary>
    public string? AccountName { get; set; }

    /// <summary>Gets or sets the day (today).</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the kind, category and account come from the last entry written with
    /// the same words (false: the defaults).
    /// </summary>
    public bool Remembered { get; set; }
}
