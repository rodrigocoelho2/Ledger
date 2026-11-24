using Ledger.Library.Enums;
using Ledger.Sdk.Models;

namespace Ledger.Website.Model;

/// <summary>
/// The tabs of the app.
/// </summary>
public enum AppSection
{
    /// <summary>The month of the person's own money.</summary>
    Overview,

    /// <summary>Every line of the ledger.</summary>
    Entries,

    /// <summary>Where the money sits.</summary>
    Accounts,

    /// <summary>The short-term rental module (optional).</summary>
    Rental,

    /// <summary>The buy-and-sell module (optional).</summary>
    Flips,

    /// <summary>Modules, language, files and the account.</summary>
    Settings,
}

/// <summary>
/// What every signed-in page knows: who is looking, which tab is open and which optional modules are on.
/// </summary>
public class AppPageModel
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Ledger";

    /// <summary>The version shown in Settings.</summary>
    public const string CurrentVersion = "v1.0";

    /// <summary>Gets or sets the signed-in user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the signed-in user is the admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets the open tab.</summary>
    public AppSection Section { get; set; }

    /// <summary>Gets or sets a value indicating whether the short-term rental module is on.</summary>
    public bool RentalEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the buy-and-sell module is on.</summary>
    public bool FlipsEnabled { get; set; }

    /// <summary>Gets or sets what the last form said went well (shown once).</summary>
    public string? Notice { get; set; }

    /// <summary>Gets or sets what the last form said went wrong (shown once).</summary>
    public string? Problem { get; set; }

    /// <summary>Gets or sets why the page has no data (the WebApi can't be reached).</summary>
    public string? Unavailable { get; set; }

    /// <summary>Gets the letter in the avatar.</summary>
    public string Initial => UserName.Length == 0 ? "L" : UserName[..1].ToUpperInvariant();

    /// <summary>Gets a value indicating whether any optional module is on (then entries say which part of life they belong to).</summary>
    public bool HasModules => RentalEnabled || FlipsEnabled;

    /// <summary>Gets or sets a value indicating whether accounts can take their value from Obol.</summary>
    public bool ObolEnabled { get; set; }
}

/// <summary>The public page that says what Ledger is.</summary>
public sealed class LandingModel
{
    /// <summary>Gets or sets a value indicating whether the visitor is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets a value indicating whether a new account can be created here.</summary>
    public bool CanCreateAccount { get; set; }
}

/// <summary>The overview of a month.</summary>
public sealed class OverviewPageModel : AppPageModel
{
    /// <summary>Gets or sets the month's numbers.</summary>
    public OverviewDTO Data { get; set; } = new();
}

/// <summary>Every entry of a month.</summary>
public sealed class EntriesPageModel : AppPageModel
{
    /// <summary>Gets or sets the entries.</summary>
    public EntryMonthDTO Data { get; set; } = new();

    /// <summary>Gets or sets the first day of the month.</summary>
    public DateOnly Month { get; set; }

    /// <summary>Gets or sets the part of life being listed (null: all).</summary>
    public EntryScope? Scope { get; set; }

    /// <summary>Gets or sets the category being listed (null: all).</summary>
    public string? Category { get; set; }
}

/// <summary>The accounts.</summary>
public sealed class AccountsPageModel : AppPageModel
{
    /// <summary>Gets or sets the accounts, in the person's order.</summary>
    public List<AccountDTO> Accounts { get; set; } = [];
}

/// <summary>A month of the short-term rental.</summary>
public sealed class RentalPageModel : AppPageModel
{
    /// <summary>Gets or sets the month's numbers.</summary>
    public RentalDTO Data { get; set; } = new();
}

/// <summary>The buy-and-sell projects.</summary>
public sealed class ProjectsPageModel : AppPageModel
{
    /// <summary>Gets or sets the projects.</summary>
    public ProjectsDTO Data { get; set; } = new();
}

/// <summary>Settings.</summary>
public sealed class SettingsPageModel : AppPageModel
{
    /// <summary>Gets or sets the signed-in user.</summary>
    public UserDTO? Me { get; set; }

    /// <summary>Gets or sets every user (only for the admin).</summary>
    public List<UserDTO> Users { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether "Sign in with Discord" is set up.</summary>
    public bool DiscordEnabled { get; set; }

    /// <summary>Gets or sets what the Discord step just did ("linked", "unlinked", "taken", "needs-password", "failed").</summary>
    public string? DiscordNote { get; set; }

    /// <summary>Gets or sets a value indicating whether the app runs on the sample ledger.</summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets the bank connections.</summary>
    public BankStatusDTO? Bank { get; set; }

    /// <summary>Gets or sets the banks that can be connected (empty when Enable Banking didn't answer).</summary>
    public List<BankChoiceDTO> Banks { get; set; } = [];

    /// <summary>Gets or sets why the bank list couldn't be read.</summary>
    public string? BanksProblem { get; set; }

    /// <summary>Gets or sets the person's accounts (to pick where a bank account goes).</summary>
    public List<AccountDTO> Accounts { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether an authorisation is under way (the person may paste where they landed).</summary>
    public bool BankPending { get; set; }
}

/// <summary>The form that adds or changes an entry.</summary>
public sealed class EntryFormModel
{
    /// <summary>Gets or sets the entry being changed (null: a new one).</summary>
    public EntryDTO? Entry { get; set; }

    /// <summary>Gets or sets the kind the form opens on.</summary>
    public EntryKind Kind { get; set; } = EntryKind.Outflow;

    /// <summary>Gets or sets the part of life the form opens on.</summary>
    public EntryScope Scope { get; set; } = EntryScope.Personal;

    /// <summary>Gets or sets the day the form opens on.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the day the first instalment is offered on: the first business day of next month.</summary>
    public DateOnly FirstInstalment { get; set; }

    /// <summary>Gets or sets the person's accounts.</summary>
    public List<AccountDTO> Accounts { get; set; } = [];

    /// <summary>Gets or sets the person's buy-and-sell projects (id and name).</summary>
    public List<(string Id, string Name)> Projects { get; set; } = [];

    /// <summary>Gets or sets the project the form opens on.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets a value indicating whether the short-term rental module is on.</summary>
    public bool RentalEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the buy-and-sell module is on.</summary>
    public bool FlipsEnabled { get; set; }
}
