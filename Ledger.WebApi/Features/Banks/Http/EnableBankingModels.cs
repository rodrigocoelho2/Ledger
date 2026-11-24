namespace Ledger.WebApi.Features.Banks.Http;

// What Enable Banking sends back (https://enablebanking.com/docs/api/reference), read with snake_case names.
// Only the fields Ledger uses are here.

/// <summary>A bank Enable Banking can reach (an ASPSP).</summary>
public sealed class EbAspsp
{
    /// <summary>Gets or sets the name, as /auth wants it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the country.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Gets or sets the longest consent the bank gives, in seconds.</summary>
    public long? MaximumConsentValidity { get; set; }

    /// <summary>Gets or sets who the bank serves ("personal", "business").</summary>
    public List<string>? PsuTypes { get; set; }
}

/// <summary>The banks of a country.</summary>
public sealed class EbAspsps
{
    /// <summary>Gets or sets the banks.</summary>
    public List<EbAspsp> Aspsps { get; set; } = [];
}

/// <summary>The Enable Banking application.</summary>
public sealed class EbApplication
{
    /// <summary>Gets or sets the addresses the bank may send the person back to.</summary>
    public List<string> RedirectUrls { get; set; } = [];
}

/// <summary>The answer to POST /auth.</summary>
public sealed class EbAuth
{
    /// <summary>Gets or sets where the person goes to authorise.</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>An account's identification.</summary>
public sealed class EbAccountId
{
    /// <summary>Gets or sets the IBAN.</summary>
    public string? Iban { get; set; }
}

/// <summary>An account the person authorised.</summary>
public sealed class EbAccount
{
    /// <summary>Gets or sets Enable Banking's id for it.</summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>Gets or sets its identification.</summary>
    public EbAccountId? AccountId { get; set; }

    /// <summary>Gets or sets what the bank calls it.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the product name.</summary>
    public string? Product { get; set; }

    /// <summary>Gets or sets the currency.</summary>
    public string? Currency { get; set; }
}

/// <summary>The bank of a session.</summary>
public sealed class EbSessionAspsp
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the country.</summary>
    public string Country { get; set; } = string.Empty;
}

/// <summary>What the consent covers.</summary>
public sealed class EbAccess
{
    /// <summary>Gets or sets when it ends.</summary>
    public DateTimeOffset? ValidUntil { get; set; }
}

/// <summary>The answer to POST /sessions.</summary>
public sealed class EbSession
{
    /// <summary>Gets or sets the session.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the accounts authorised.</summary>
    public List<EbAccount> Accounts { get; set; } = [];

    /// <summary>Gets or sets the bank.</summary>
    public EbSessionAspsp? Aspsp { get; set; }

    /// <summary>Gets or sets what the consent covers.</summary>
    public EbAccess? Access { get; set; }
}

/// <summary>An amount, as text.</summary>
public sealed class EbAmount
{
    /// <summary>Gets or sets the amount ("12.34").</summary>
    public string Amount { get; set; } = "0";

    /// <summary>Gets or sets the currency.</summary>
    public string? Currency { get; set; }
}

/// <summary>A balance.</summary>
public sealed class EbBalance
{
    /// <summary>Gets or sets the amount.</summary>
    public EbAmount? BalanceAmount { get; set; }

    /// <summary>Gets or sets the kind (CLBD booked, ITAV available…).</summary>
    public string? BalanceType { get; set; }
}

/// <summary>The balances of an account.</summary>
public sealed class EbBalances
{
    /// <summary>Gets or sets the balances.</summary>
    public List<EbBalance> Balances { get; set; } = [];
}

/// <summary>The other side of a transaction.</summary>
public sealed class EbParty
{
    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }
}

/// <summary>A transaction.</summary>
public sealed class EbTransaction
{
    /// <summary>Gets or sets the bank's reference.</summary>
    public string? EntryReference { get; set; }

    /// <summary>Gets or sets Enable Banking's id.</summary>
    public string? TransactionId { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public EbAmount? TransactionAmount { get; set; }

    /// <summary>Gets or sets who received the money.</summary>
    public EbParty? Creditor { get; set; }

    /// <summary>Gets or sets who paid.</summary>
    public EbParty? Debtor { get; set; }

    /// <summary>Gets or sets CRDT (money in) or DBIT (money out).</summary>
    public string? CreditDebitIndicator { get; set; }

    /// <summary>Gets or sets BOOK, PDNG…</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the day it was booked (yyyy-MM-dd).</summary>
    public string? BookingDate { get; set; }

    /// <summary>Gets or sets the value day.</summary>
    public string? ValueDate { get; set; }

    /// <summary>Gets or sets the day it was made.</summary>
    public string? TransactionDate { get; set; }

    /// <summary>Gets or sets the description lines.</summary>
    public List<string>? RemittanceInformation { get; set; }
}

/// <summary>A page of transactions.</summary>
public sealed class EbTransactions
{
    /// <summary>Gets or sets the transactions.</summary>
    public List<EbTransaction> Transactions { get; set; } = [];

    /// <summary>Gets or sets the key of the next page (null: the last one).</summary>
    public string? ContinuationKey { get; set; }
}

/// <summary>Enable Banking said no, or said something Ledger couldn't read.</summary>
/// <param name="message">What happened.</param>
public sealed class BankException(string message) : Exception(message);
