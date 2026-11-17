using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Overview.Models.DTOs;

namespace Ledger.WebApi.Features.Projects.Models.DTOs;

/// <summary>One stretch of a project's life, between two things that happened.</summary>
public sealed class PhaseDTO
{
    /// <summary>Gets or sets what the stretch is: "deposit", "deed", "works" or "market".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the day it started.</summary>
    public DateOnly Start { get; set; }

    /// <summary>Gets or sets the day it ended (today, for the last stretch of a project still in progress).</summary>
    public DateOnly End { get; set; }

    /// <summary>Gets or sets how many days it took.</summary>
    public int Days { get; set; }
}

/// <summary>A property bought to renovate and sell, with everything worked out from its entries.</summary>
public sealed class ProjectDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether it is sold.</summary>
    public ProjectState State { get; set; }

    /// <summary>Gets or sets everything put in (loan repayments left out).</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets what it sold for (0: not sold).</summary>
    public decimal Sold { get; set; }

    /// <summary>Gets or sets the profit or loss (only once sold).</summary>
    public decimal? Result { get; set; }

    /// <summary>Gets or sets the result over what was put in (only once sold).</summary>
    public decimal? Return { get; set; }

    /// <summary>Gets or sets the price paid for the property itself.</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>Gets or sets what the works and the labour came to so far.</summary>
    public decimal WorksSpent { get; set; }

    /// <summary>Gets or sets the day of its first entry.</summary>
    public DateOnly? FirstDay { get; set; }

    /// <summary>Gets or sets the day it sold.</summary>
    public DateOnly? SaleDay { get; set; }

    /// <summary>Gets or sets the days from the first entry to the sale (or to today, while in progress).</summary>
    public int? Days { get; set; }

    /// <summary>Gets or sets how many entries it has.</summary>
    public int EntryCount { get; set; }

    /// <summary>Gets or sets the price the person hopes to sell for.</summary>
    public decimal? TargetSale { get; set; }

    /// <summary>Gets or sets what the works should cost.</summary>
    public decimal? WorksBudget { get; set; }

    /// <summary>Gets or sets what the project should cost in the end: what was put in plus what is left of the works budget.</summary>
    public decimal? ProjectedCost { get; set; }

    /// <summary>Gets or sets the profit the target sale price would leave.</summary>
    public decimal? TargetResult { get; set; }

    /// <summary>Gets or sets that profit over the projected cost.</summary>
    public decimal? TargetReturn { get; set; }

    /// <summary>Gets or sets the stretches of its life, in order.</summary>
    public List<PhaseDTO> Phases { get; set; } = [];

    /// <summary>
    /// Gets or sets where the money went: each cost category and, once sold at a profit, the profit (key "profit"),
    /// as shares of the sale price. Not sold, or sold at a loss: shares of what was put in.
    /// </summary>
    public List<FlowPartDTO> Split { get; set; } = [];

    /// <summary>Gets or sets its entries, newest first.</summary>
    public List<EntryDTO> Entries { get; set; } = [];
}

/// <summary>The buy-and-sell module: every project and what they add up to.</summary>
public sealed class ProjectsDTO
{
    /// <summary>Gets or sets the profit of the projects already sold.</summary>
    public decimal RealisedProfit { get; set; }

    /// <summary>Gets or sets how many are sold.</summary>
    public int SoldCount { get; set; }

    /// <summary>Gets or sets what was put into the sold ones.</summary>
    public decimal InvestedInSold { get; set; }

    /// <summary>Gets or sets the return on the sold ones (null: none sold). Projects in progress are left out.</summary>
    public decimal? ReturnOnSold { get; set; }

    /// <summary>Gets or sets what is tied up in the projects in progress.</summary>
    public decimal CapitalAtWork { get; set; }

    /// <summary>Gets or sets how many are in progress.</summary>
    public int OpenCount { get; set; }

    /// <summary>Gets or sets the days from first entry to sale, on average over the sold ones (null: none sold).</summary>
    public int? AverageDays { get; set; }

    /// <summary>Gets or sets the projects, in progress first, then the latest sale.</summary>
    public List<ProjectDTO> Projects { get; set; } = [];

    /// <summary>Gets or sets the buy-and-sell entries that name no project, newest first.</summary>
    public List<EntryDTO> Loose { get; set; } = [];
}

/// <summary>Adds a project (no <see cref="Id"/>) or changes one.</summary>
public sealed class ProjectSaveRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the project to change (empty: a new one).</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the price hoped for (empty: none).</summary>
    public decimal? TargetSale { get; set; }

    /// <summary>Gets or sets what the works should cost (empty: none).</summary>
    public decimal? WorksBudget { get; set; }
}
