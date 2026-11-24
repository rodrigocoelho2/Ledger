using System.Text.Json.Serialization;

namespace Ledger.WebApi.Features.Entries.Models.Entities;

/// <summary>
/// One line of the ledger (dbo.Entry), with the public identifiers of what it points at. The short JSON names are the
/// ones dbo.EntryImport reads.
/// </summary>
public sealed class EntryRow
{
    /// <summary>Gets or sets the public identifier.</summary>
    [JsonIgnore]
    public Guid EntryUniqueID { get; set; }

    /// <summary>Gets or sets the kind (<see cref="Ledger.Library.Enums.EntryKind"/>).</summary>
    [JsonPropertyName("k")]
    public byte Kind { get; set; }

    /// <summary>Gets or sets the scope (<see cref="Ledger.Library.Enums.EntryScope"/>).</summary>
    [JsonPropertyName("s")]
    public byte Scope { get; set; }

    /// <summary>Gets or sets what it was.</summary>
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount (always positive).</summary>
    [JsonPropertyName("a")]
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the day (for a booking, the check-in).</summary>
    [JsonIgnore]
    public DateTime EntryDate { get; set; }

    /// <summary>Gets the day as dbo.EntryImport reads it (yyyy-MM-dd).</summary>
    [JsonPropertyName("d")]
    public string DayText => EntryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets or sets the category key (null on transfers).</summary>
    [JsonPropertyName("c")]
    public string? Category { get; set; }

    /// <summary>Gets or sets the account it left or came into (null: the account was deleted).</summary>
    [JsonPropertyName("ac")]
    public Guid? AccountUniqueID { get; set; }

    /// <summary>Gets or sets the account a transfer went to.</summary>
    [JsonPropertyName("to")]
    public Guid? ToAccountUniqueID { get; set; }

    /// <summary>Gets or sets the buy-and-sell project it belongs to.</summary>
    [JsonPropertyName("p")]
    public Guid? ProjectUniqueID { get; set; }

    /// <summary>Gets or sets the nights of a rental booking.</summary>
    [JsonPropertyName("ni")]
    public short? Nights { get; set; }

    /// <summary>Gets or sets where a rental booking came from (Airbnb, Booking, direct…).</summary>
    [JsonPropertyName("ch")]
    public string? Channel { get; set; }

    /// <summary>Gets or sets when it was written (UTC).</summary>
    [JsonIgnore]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instalment plan the entry belongs to (null: none).</summary>
    [JsonPropertyName("pk")]
    public Guid? PlanKey { get; set; }

    /// <summary>Gets or sets which instalment of the plan it is (1, 2, 3…).</summary>
    [JsonPropertyName("pp")]
    public short? PlanPart { get; set; }

    /// <summary>Gets or sets how many instalments the plan has.</summary>
    [JsonPropertyName("pn")]
    public short? PlanParts { get; set; }

    /// <summary>Gets the day as a date.</summary>
    [JsonIgnore]
    public DateOnly Day => DateOnly.FromDateTime(EntryDate);
}
