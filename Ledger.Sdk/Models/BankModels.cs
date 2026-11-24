namespace Ledger.Sdk.Models;

/// <summary>A bank account the person connected.</summary>
public sealed class BankLinkDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank.</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Gets or sets what the bank calls the account.</summary>
    public string? AccountLabel { get; set; }

    /// <summary>Gets or sets the last four digits of the IBAN.</summary>
    public string? IbanTail { get; set; }

    /// <summary>Gets or sets the Ledger account it goes into (null: not picked yet).</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets that account's name.</summary>
    public string? AccountName { get; set; }

    /// <summary>Gets or sets when the bank's consent ends.</summary>
    public DateTimeOffset? ValidUntil { get; set; }

    /// <summary>Gets or sets a value indicating whether the consent has ended (connect again to renew it).</summary>
    public bool Expired { get; set; }

    /// <summary>Gets or sets the first day brought in.</summary>
    public DateOnly? ImportFrom { get; set; }

    /// <summary>Gets or sets when the last sync worked.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Gets or sets why the last sync failed ("expired" when the consent ended).</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets the balance the bank reported last.</summary>
    public decimal? BankBalance { get; set; }

    /// <summary>Gets or sets the balance in Ledger.</summary>
    public decimal? LedgerBalance { get; set; }

    /// <summary>Gets or sets what Ledger has more than the bank (0 when they agree; null when unknown).</summary>
    public decimal? Difference { get; set; }
}

/// <summary>Bank connections of a person.</summary>
public sealed class BankStatusDTO
{
    /// <summary>Gets or sets a value indicating whether this instance can connect to banks.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the connected accounts.</summary>
    public List<BankLinkDTO> Links { get; set; } = [];

    /// <summary>Gets or sets how many hours apart the accounts sync on their own.</summary>
    public int SyncHours { get; set; }
}

/// <summary>A bank the person can connect.</summary>
public sealed class BankChoiceDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the country.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Gets or sets the longest consent, in days.</summary>
    public int? ConsentDays { get; set; }
}

/// <summary>Starts connecting a bank.</summary>
public sealed class BankStartRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the bank's name.</summary>
    public string? Bank { get; set; }

    /// <summary>Gets or sets the bank's country (default PT).</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets where the Website would like the person back (used when it is registered at Enable Banking).</summary>
    public string? RedirectUrl { get; set; }
}

/// <summary>Where to send the person.</summary>
public sealed class BankStartDTO
{
    /// <summary>Gets or sets the bank's page.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets what comes back with the person.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Gets or sets where the bank sends the person back.</summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the person comes back somewhere else than Ledger, and has to paste
    /// the address they land on.
    /// </summary>
    public bool Paste { get; set; }
}

/// <summary>The bank sent the person back.</summary>
public sealed class BankCompleteRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the code (or the whole address the person landed on).</summary>
    public string? Code { get; set; }

    /// <summary>Gets or sets the state that came back.</summary>
    public string? State { get; set; }
}

/// <summary>Says which Ledger account a bank account goes into.</summary>
public sealed class BankAssignRequest
{
    /// <summary>The value of <see cref="AccountId"/> that makes a new account.</summary>
    public const string NewAccount = "new";

    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the link.</summary>
    public string? LinkId { get; set; }

    /// <summary>Gets or sets the Ledger account, "new" for a new one, or empty to stop syncing.</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets the first day to bring (default: the first of this month).</summary>
    public DateOnly? ImportFrom { get; set; }
}

/// <summary>What a sync did.</summary>
public sealed class BankSyncDTO
{
    /// <summary>Gets or sets the new entries (to review).</summary>
    public int Imported { get; set; }

    /// <summary>Gets or sets the entries written by hand that the bank confirmed.</summary>
    public int Matched { get; set; }

    /// <summary>Gets or sets the link after the sync.</summary>
    public BankLinkDTO? Link { get; set; }
}

/// <summary>Makes Ledger's balance agree with the bank's, by changing the opening balance.</summary>
public sealed class BankAlignRequest
{
    /// <summary>Gets or sets the user.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the link.</summary>
    public string? LinkId { get; set; }
}
