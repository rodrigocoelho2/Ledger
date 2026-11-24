namespace Ledger.WebApi.Features.Banks.Models.Entities;

/// <summary>
/// A bank account a person connected through Enable Banking (dbo.BankLink).
/// </summary>
public sealed class BankLinkRow
{
    /// <summary>Gets or sets the link's id.</summary>
    public Guid BankLinkUniqueID { get; set; }

    /// <summary>Gets or sets the person.</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets the Ledger account its transactions go into (null: not picked yet).</summary>
    public Guid? AccountUniqueID { get; set; }

    /// <summary>Gets or sets Enable Banking's session.</summary>
    public string SessionID { get; set; } = string.Empty;

    /// <summary>Gets or sets Enable Banking's id for the account.</summary>
    public string AccountUid { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank's name.</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank's country (ISO 3166, two letters).</summary>
    public string BankCountry { get; set; } = "PT";

    /// <summary>Gets or sets what the bank calls the account.</summary>
    public string? AccountLabel { get; set; }

    /// <summary>Gets or sets the last four digits of the IBAN.</summary>
    public string? IbanTail { get; set; }

    /// <summary>Gets or sets when the bank's consent ends (UTC).</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Gets or sets the first day to bring transactions from.</summary>
    public DateTime? ImportFrom { get; set; }

    /// <summary>Gets or sets when the last sync worked (UTC).</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>Gets or sets why the last sync failed (null: it worked).</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets the balance the bank reported on the last sync.</summary>
    public decimal? BankBalance { get; set; }

    /// <summary>Gets or sets when it was connected (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
