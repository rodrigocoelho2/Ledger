namespace Ledger.WebApi.Features.Overview.Models.Entities;

/// <summary>
/// The spending cap of a month (dbo.MonthCap).
/// </summary>
public sealed class CapRow
{
    /// <summary>Gets or sets the first day of the month.</summary>
    public DateTime MonthStart { get; set; }

    /// <summary>Gets or sets the most the person wants to spend in it.</summary>
    public decimal Amount { get; set; }
}
