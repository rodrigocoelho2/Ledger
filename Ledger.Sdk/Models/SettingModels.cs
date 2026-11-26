namespace Ledger.Sdk.Models;

/// <summary>What a person chose in Settings.</summary>
public sealed class SettingSaveRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the language: "en" or "pt" (empty: leave it as it is).</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets whether the short-term rental module is on (empty: leave it as it is).</summary>
    public bool? RentalEnabled { get; set; }

    /// <summary>Gets or sets whether the buy-and-sell module is on (empty: leave it as it is).</summary>
    public bool? FlipsEnabled { get; set; }

    /// <summary>Gets or sets the day of the month the salary is due, 2 to 31; 0 or 1: calendar months (empty: leave it as it is).</summary>
    public int? PayDay { get; set; }

    /// <summary>Gets or sets a word of the salary's bank description, such as the employer ("": none; empty: leave it as it is).</summary>
    public string? Payer { get; set; }
}
