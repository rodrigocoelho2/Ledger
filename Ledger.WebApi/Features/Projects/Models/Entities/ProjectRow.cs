namespace Ledger.WebApi.Features.Projects.Models.Entities;

/// <summary>
/// A property bought to renovate and sell (dbo.Project).
/// </summary>
public sealed class ProjectRow
{
    /// <summary>Gets or sets the public identifier.</summary>
    public Guid ProjectUniqueID { get; set; }

    /// <summary>Gets or sets the name (unique per person, ignoring case).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the price the person hopes to sell for.</summary>
    public decimal? TargetSale { get; set; }

    /// <summary>Gets or sets what the person plans to spend on the works.</summary>
    public decimal? WorksBudget { get; set; }

    /// <summary>Gets or sets the price agreed with the seller.</summary>
    public decimal? AgreedPrice { get; set; }

    /// <summary>Gets or sets when it was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
