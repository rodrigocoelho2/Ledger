namespace Ledger.WebApi.Features;

/// <summary>
/// Module options, bound from the "Ledger" configuration section (in Docker: Ledger__ApiKey, Ledger__TimeZone…).
/// </summary>
public sealed class LedgerOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Ledger";

    /// <summary>Gets or sets the key the Website must send in the x-api-key header. Never commit a real value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the time zone "today" and "this month" are counted in.</summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>
    /// Gets or sets a value indicating whether the app runs without SQL Server, on a sample ledger kept in memory
    /// (a "demo" user is created on start, and everything is lost on restart).
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets the password of the "demo" user in demo mode.</summary>
    public string DemoPassword { get; set; } = "ledger-demo";
}
