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

    /// <summary>
    /// Gets or sets the Enable Banking application id (control panel → Applications). Empty: no bank connections.
    /// </summary>
    public string EnableBankingAppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application's private key: the PEM the control panel gave, as is or in base64 (one line fits
    /// an .env). Never commit a real value.
    /// </summary>
    public string EnableBankingKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Enable Banking API.</summary>
    public string EnableBankingUrl { get; set; } = "https://api.enablebanking.com";

    /// <summary>
    /// Gets or sets how many hours apart a connected account is synced on its own. Banks allow about four syncs a day
    /// without the person present (PSD2), so less than 6 is refused.
    /// </summary>
    public int BankSyncHours { get; set; } = 8;

    /// <summary>Gets or sets Obol's WebApi (on the NAS, over the shared Docker network). Empty: no Obol.</summary>
    public string ObolUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the key Obol's WebApi expects in the x-api-key header (OBOL_API_KEY in Obol's .env).</summary>
    public string ObolApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets how many minutes apart the values from Obol are refreshed.</summary>
    public int ObolMinutes { get; set; } = 30;

    /// <summary>Gets a value indicating whether bank connections are set up.</summary>
    public bool BankEnabled => !string.IsNullOrWhiteSpace(EnableBankingAppId) && !string.IsNullOrWhiteSpace(EnableBankingKey);

    /// <summary>Gets a value indicating whether Obol is set up.</summary>
    public bool ObolEnabled => !string.IsNullOrWhiteSpace(ObolUrl) && !string.IsNullOrWhiteSpace(ObolApiKey);
}
