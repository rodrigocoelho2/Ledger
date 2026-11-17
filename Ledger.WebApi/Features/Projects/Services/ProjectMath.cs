using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Models.Entities;
using Ledger.WebApi.Features.Overview.Models.DTOs;
using Ledger.WebApi.Features.Overview.Services;
using Ledger.WebApi.Features.Projects.Models.DTOs;
using Ledger.WebApi.Features.Projects.Models.Entities;

namespace Ledger.WebApi.Features.Projects.Services;

/// <summary>
/// Works a buy-and-sell project out from its entries. No database; today comes in as a parameter.
/// </summary>
internal static class ProjectMath
{
    /// <summary>The key of the profit slice in a sold project's split.</summary>
    public const string Profit = "profit";

    /// <summary>
    /// Whether an entry is a cost of the project: any outflow except loan repayments, which move the financing and
    /// would count the property twice.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>True for a cost.</returns>
    public static bool IsCost(EntryRow entry) =>
        entry.Kind == (byte)EntryKind.Outflow && !string.Equals(entry.Category, Categories.Loans, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether an entry is the sale (or part of it).</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>True for a sale.</returns>
    public static bool IsSale(EntryRow entry) =>
        entry.Kind == (byte)EntryKind.Inflow && string.Equals(entry.Category, Categories.Sale, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Works a project out.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="entries">Its entries (any order).</param>
    /// <param name="today">Today, for the days of a project still in progress.</param>
    /// <returns>The project, without its entries listed.</returns>
    public static ProjectDTO Build(ProjectRow project, IReadOnlyCollection<EntryRow> entries, DateOnly today)
    {
        var costs = entries.Where(IsCost).ToList();
        var sales = entries.Where(IsSale).ToList();
        var invested = costs.Sum(static entry => entry.Amount);
        var sold = sales.Sum(static entry => entry.Amount);
        var isSold = sales.Count > 0;

        DateOnly? firstDay = entries.Count == 0 ? null : entries.Min(static entry => entry.Day);
        DateOnly? saleDay = isSold ? sales.Max(static entry => entry.Day) : null;
        var worksSpent = costs.Where(IsWorks).Sum(static entry => entry.Amount);

        var dto = new ProjectDTO
        {
            Id = project.ProjectUniqueID.ToString("N"),
            Name = project.Name,
            State = isSold ? ProjectState.Sold : ProjectState.InProgress,
            Invested = invested,
            Sold = sold,
            Result = isSold ? sold - invested : null,
            Return = isSold && invested > 0 ? OverviewMath.Share(sold - invested, invested) : null,
            PurchasePrice = costs.Where(static entry => Is(entry, Categories.Purchase)).Sum(static entry => entry.Amount),
            WorksSpent = worksSpent,
            FirstDay = firstDay,
            SaleDay = saleDay,
            Days = firstDay is { } first ? Math.Max(0, (saleDay ?? today).DayNumber - first.DayNumber) : null,
            EntryCount = entries.Count,
            TargetSale = project.TargetSale,
            WorksBudget = project.WorksBudget,
            Phases = Phases(costs, firstDay, saleDay, today),
            Split = Split(costs, invested, sold, isSold),
        };

        if (!isSold && project.TargetSale is { } target)
        {
            var projected = invested + Math.Max(0m, (project.WorksBudget ?? 0m) - worksSpent);
            dto.ProjectedCost = projected;
            dto.TargetResult = target - projected;
            dto.TargetReturn = projected > 0 ? OverviewMath.Share(target - projected, projected) : null;
        }

        return dto;
    }

    /// <summary>
    /// The stretches of a project's life, from what its entries say: the deposit until the deed, the deed until the
    /// works start, the works, and the time on the market until the sale. A stretch nothing marks is left out, and the
    /// last one of a project still in progress runs until today.
    /// </summary>
    /// <param name="costs">The project's costs.</param>
    /// <param name="firstDay">The day of its first entry.</param>
    /// <param name="saleDay">The day it sold (null: not yet).</param>
    /// <param name="today">Today.</param>
    /// <returns>The stretches, in order.</returns>
    public static List<PhaseDTO> Phases(IReadOnlyCollection<EntryRow> costs, DateOnly? firstDay, DateOnly? saleDay, DateOnly today)
    {
        if (firstDay is not { } first)
        {
            return [];
        }

        var until = saleDay ?? (today < first ? first : today);

        // The deed: the day the taxes and the deed were paid, or else the last payment of the price itself.
        var deedDays = costs.Where(static entry => Is(entry, Categories.Deed)).Select(static entry => entry.Day).ToList();
        var purchaseDays = costs.Where(static entry => Is(entry, Categories.Purchase)).Select(static entry => entry.Day).Distinct().ToList();
        DateOnly? deed = deedDays.Count > 0 ? deedDays.Min() : purchaseDays.Count > 1 ? purchaseDays.Max() : null;

        var worksDays = costs.Where(IsWorks).Select(static entry => entry.Day).Where(day => day <= until).ToList();
        DateOnly? worksStart = worksDays.Count > 0 ? worksDays.Min() : null;
        DateOnly? worksEnd = worksDays.Count > 0 ? worksDays.Max() : null;

        // The marks in order; one that falls before the mark behind it is dropped.
        var marks = new List<(string Key, DateOnly Day)> { ("deposit", first) };
        Add("deed", deed);
        Add("works", worksStart);
        if (saleDay is not null)
        {
            Add("market", worksEnd);
        }

        var phases = new List<PhaseDTO>();
        for (var index = 0; index < marks.Count; index++)
        {
            var end = index + 1 < marks.Count ? marks[index + 1].Day : until;
            if (end > marks[index].Day)
            {
                phases.Add(new PhaseDTO { Key = marks[index].Key, Start = marks[index].Day, End = end, Days = end.DayNumber - marks[index].Day.DayNumber });
            }
        }

        return phases;

        void Add(string key, DateOnly? day)
        {
            if (day is { } value && value > marks[^1].Day && value <= until)
            {
                marks.Add((key, value));
            }
            else if (day is { } same && same == marks[^1].Day && marks.Count == 1)
            {
                // The very first entry is already this mark (a purchase paid in one go at the deed): rename the start.
                marks[0] = (key, same);
            }
        }
    }

    /// <summary>
    /// Where the money went. Sold at a profit: each cost and the profit as shares of the sale price. Otherwise: each
    /// cost as a share of what was put in.
    /// </summary>
    /// <param name="costs">The project's costs.</param>
    /// <param name="invested">Their sum.</param>
    /// <param name="sold">What it sold for.</param>
    /// <param name="isSold">Whether it sold.</param>
    /// <returns>The slices, biggest cost first and the profit last.</returns>
    public static List<FlowPartDTO> Split(IReadOnlyCollection<EntryRow> costs, decimal invested, decimal sold, bool isSold)
    {
        var parts = OverviewMath.Parts(costs, EntryKind.Outflow);
        var profit = isSold && sold > invested;
        var whole = profit ? sold : invested;
        foreach (var part in parts)
        {
            part.Share = OverviewMath.Share(part.Amount, whole);
        }

        if (profit)
        {
            parts.Add(new FlowPartDTO { Key = Profit, Amount = sold - invested, Share = OverviewMath.Share(sold - invested, whole) });
        }

        return parts;
    }

    private static bool Is(EntryRow entry, string category) => string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase);

    private static bool IsWorks(EntryRow entry) => Is(entry, Categories.Works) || Is(entry, Categories.Labour);
}
