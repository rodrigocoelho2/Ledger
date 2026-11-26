using Ledger.WebApi.Features.Accounts.Models.DTOs;
using Ledger.WebApi.Features.Entries.Models.DTOs;

namespace Ledger.WebApi.Features.Overview.Models.DTOs;

/// <summary>One slice of money: a category and what it added up to.</summary>
public sealed class FlowPartDTO
{
    /// <summary>Gets or sets the category key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets its share of the whole (0 to 1).</summary>
    public decimal Share { get; set; }
}

/// <summary>What a month came to.</summary>
public sealed class MonthNetDTO
{
    /// <summary>Gets or sets the month (yyyy-MM).</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Gets or sets the first day of the month (the payday when months follow the salary).</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the last day of the month.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets what came in minus what went out.</summary>
    public decimal Net { get; set; }

    /// <summary>Gets or sets a value indicating whether the month has any entry.</summary>
    public bool HasEntries { get; set; }

    /// <summary>Gets or sets a value indicating whether the month isn't over yet.</summary>
    public bool InProgress { get; set; }

    /// <summary>Gets or sets a value indicating whether the month is still to come.</summary>
    public bool Future { get; set; }
}

/// <summary>The overview of a month of the person's own money.</summary>
public sealed class OverviewDTO
{
    /// <summary>Gets or sets the month (yyyy-MM).</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Gets or sets the first day of the month (the payday when months follow the salary).</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the last day of the month.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets the month before it (yyyy-MM).</summary>
    public string PreviousMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets the month after it (yyyy-MM).</summary>
    public string NextMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether it is the month we are in.</summary>
    public bool IsCurrentMonth { get; set; }

    /// <summary>Gets or sets what came in.</summary>
    public decimal In { get; set; }

    /// <summary>Gets or sets what went out.</summary>
    public decimal Out { get; set; }

    /// <summary>Gets or sets what was left (negative: more went out than came in).</summary>
    public decimal Net { get; set; }

    /// <summary>Gets or sets the share of what came in that was kept, one-offs left out (null: nothing came in).</summary>
    public decimal? SavingsRate { get; set; }

    /// <summary>Gets or sets what the month before came to (null: it has no entries).</summary>
    public decimal? PreviousNet { get; set; }

    /// <summary>Gets or sets the sum of every account's balance today.</summary>
    public decimal NetWorth { get; set; }

    /// <summary>Gets or sets the spending cap of the month (null: none).</summary>
    public decimal? Cap { get; set; }

    /// <summary>Gets or sets how much of the cap is spent (1 = all of it; null: no cap).</summary>
    public decimal? CapUsed { get; set; }

    /// <summary>Gets or sets the months around this one, oldest first, for the month strip.</summary>
    public List<MonthNetDTO> Months { get; set; } = [];

    /// <summary>Gets or sets where the money came from, biggest first.</summary>
    public List<FlowPartDTO> Sources { get; set; } = [];

    /// <summary>Gets or sets where the money went, biggest first.</summary>
    public List<FlowPartDTO> Targets { get; set; } = [];

    /// <summary>Gets or sets the accounts, each with what the month added or took.</summary>
    public List<AccountDTO> Accounts { get; set; } = [];

    /// <summary>Gets or sets the latest entries of the month.</summary>
    public List<EntryDTO> Entries { get; set; } = [];

    /// <summary>Gets or sets how many entries the month has.</summary>
    public int EntryCount { get; set; }

    /// <summary>Gets or sets what came in this month from one-offs (selling a car or a motorbike).</summary>
    public decimal OneOffIn { get; set; }

    /// <summary>Gets or sets what went out this month on one-offs (buying a car or a motorbike).</summary>
    public decimal OneOffOut { get; set; }

    /// <summary>Gets or sets the instalment plans with instalments still to come, the next one first.</summary>
    public List<PlanDTO> Plans { get; set; } = [];

    /// <summary>Gets or sets what is still to pay across those plans.</summary>
    public decimal PlansLeft { get; set; }
}

/// <summary>
/// A purchase paid in instalments, from today on.
/// </summary>
public sealed class PlanDTO
{
    /// <summary>Gets or sets the plan's id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets what was bought.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the category key.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the account the instalments come out of.</summary>
    public string? AccountName { get; set; }

    /// <summary>Gets or sets how many instalments the plan has.</summary>
    public int Parts { get; set; }

    /// <summary>Gets or sets how many are still to come.</summary>
    public int Left { get; set; }

    /// <summary>Gets or sets what is still to pay.</summary>
    public decimal LeftAmount { get; set; }

    /// <summary>Gets or sets the day of the next instalment.</summary>
    public DateOnly NextDay { get; set; }

    /// <summary>Gets or sets the amount of the next instalment.</summary>
    public decimal NextAmount { get; set; }

    /// <summary>Gets or sets the day of the last instalment.</summary>
    public DateOnly LastDay { get; set; }
}

/// <summary>Sets the spending cap of a month, or removes it.</summary>
public sealed class CapSaveRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the month (yyyy-MM).</summary>
    public string? Month { get; set; }

    /// <summary>Gets or sets the cap (empty or zero: none).</summary>
    public decimal? Amount { get; set; }
}
