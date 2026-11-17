using System.Globalization;
using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Money;
using Ledger.Sdk;
using Ledger.Sdk.Models;
using Ledger.Website.Helpers;
using Ledger.Website.Model;
using Ledger.Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ledger.Website.Controllers;

/// <summary>
/// The landing page (signed out) and the overview of a month (signed in).
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
/// <param name="options">Sign-in settings.</param>
public sealed class OverviewController(LedgerClient apiClient, Lang lang, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>The overview of a month, or the landing page for a visitor.</summary>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The page.</returns>
    [HttpGet("/")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? month = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("Landing", new LandingModel { CanCreateAccount = options.Value.AllowRegistration });
        }

        return await ScreenAsync("Index", Shell(new OverviewPageModel(), AppSection.Overview), async page =>
            page.Data = await Api.OverviewGetAsync(UserId, month));
    }

    /// <summary>What Ledger is, also for someone signed in.</summary>
    /// <returns>The landing page.</returns>
    [HttpGet("/about")]
    [AllowAnonymous]
    public IActionResult Landing() => View("Landing", new LandingModel
    {
        SignedIn = User.Identity?.IsAuthenticated == true,
        CanCreateAccount = options.Value.AllowRegistration,
    });

    /// <summary>Switches the language of the pages before signing in (remembered on this device).</summary>
    /// <param name="code">"en" or "pt".</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <returns>A redirect back.</returns>
    [HttpGet("/language/{code}")]
    [AllowAnonymous]
    public IActionResult Language(string code, string? returnUrl = null)
    {
        LedgerAuthentication.Remember(HttpContext, Languages.Normalize(code));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    /// <summary>Sets the spending cap of a month, or removes it.</summary>
    /// <param name="month">The month (yyyy-MM).</param>
    /// <param name="amount">The cap as typed (empty: none).</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/cap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cap([FromForm] string? month, [FromForm] string? amount)
    {
        decimal? cap = null;
        if (!string.IsNullOrWhiteSpace(amount))
        {
            if (!Amounts.TryParse(amount, out var parsed))
            {
                return Invalid("Amount", L.T("The cap must be a number.", "O teto tem de ser um número."));
            }

            cap = parsed;
        }

        return await AnswerAsync(() => Api.OverviewCapSaveAsync(new CapSaveRequest { UserId = UserId, Month = month, Amount = cap }));
    }
}

/// <summary>
/// The entries: the month's list, the form that adds or changes one, and quick add.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
public sealed class EntriesController(LedgerClient apiClient, Lang lang) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>Every entry of a month, optionally of one part of life and one category.</summary>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <param name="scope">"personal", "rental" or "flips" (empty: all).</param>
    /// <param name="category">A category key, or "transfers" (empty: all).</param>
    /// <returns>The page.</returns>
    [HttpGet("/entries")]
    public async Task<IActionResult> Index(string? month = null, string? scope = null, string? category = null)
    {
        var page = Shell(new EntriesPageModel(), AppSection.Entries);
        page.Scope = Enum.TryParse<EntryScope>(scope, ignoreCase: true, out var part) && Enum.IsDefined(part) ? part : null;

        // A module that is switched off doesn't show up in the list either: its entries are kept, out of the way.
        if ((page.Scope == EntryScope.Rental && !page.RentalEnabled) || (page.Scope == EntryScope.Flips && !page.FlipsEnabled))
        {
            page.Scope = null;
        }

        if (!page.HasModules)
        {
            page.Scope = EntryScope.Personal;
        }

        page.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        return await ScreenAsync("Index", page, async model =>
        {
            model.Data = await Api.EntryMonthAsync(UserId, month, model.Scope, model.Category);
            model.Month = DateOnly.ParseExact(model.Data.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (model.Scope is null)
            {
                // Everything, minus what belongs to a module that is off.
                model.Data.Entries.RemoveAll(entry =>
                    (entry.Scope == EntryScope.Rental && !model.RentalEnabled) || (entry.Scope == EntryScope.Flips && !model.FlipsEnabled));
            }
        });
    }

    /// <summary>The form that adds or changes an entry (the page script puts it in a dialog).</summary>
    /// <param name="id">The entry to change (empty: a new one).</param>
    /// <param name="scope">The part of life a new entry opens on.</param>
    /// <param name="kind">The kind a new entry opens on.</param>
    /// <param name="project">The project a new buy-and-sell entry opens on.</param>
    /// <param name="text">A quick-add line to start from.</param>
    /// <returns>The form.</returns>
    [HttpGet("/entries/form")]
    public async Task<IActionResult> Form(string? id = null, string? scope = null, string? kind = null, string? project = null, string? text = null)
    {
        var form = new EntryFormModel
        {
            RentalEnabled = LedgerAuthentication.Rental(User),
            FlipsEnabled = LedgerAuthentication.Flips(User),
            Day = Clock.Today,
            ProjectId = project,
        };

        try
        {
            form.Accounts = await Api.AccountListAsync(UserId);
            if (form.FlipsEnabled)
            {
                form.Projects = [.. (await Api.ProjectListAsync(UserId)).Projects.Select(static item => (item.Id, item.Name))];
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                form.Entry = await Api.EntryGetAsync(UserId, id);
                form.Kind = form.Entry.Kind;
                form.Scope = form.Entry.Scope;
                form.Day = form.Entry.Date;
                form.ProjectId = form.Entry.ProjectId;
            }
            else
            {
                form.Kind = Enum.TryParse<EntryKind>(kind, ignoreCase: true, out var wanted) && Enum.IsDefined(wanted) ? wanted : EntryKind.Outflow;
                form.Scope = Enum.TryParse<EntryScope>(scope, ignoreCase: true, out var part) && Enum.IsDefined(part) ? part : EntryScope.Personal;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Start from what quick add understood; the person changes what it got wrong.
                    var quick = await Api.EntryQuickAsync(UserId, text);
                    form.Kind = quick.Kind;
                    form.Scope = quick.Scope;
                    form.Entry = new EntryDTO
                    {
                        Kind = quick.Kind,
                        Scope = quick.Scope,
                        Name = quick.Name,
                        Amount = quick.Amount,
                        Date = quick.Date,
                        Category = quick.Category,
                        AccountId = quick.AccountId,
                    };
                }
            }
        }
        catch (LedgerApiException exception) when (exception.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
        {
            if (form.Accounts.Count == 0)
            {
                return NotFound();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or LedgerApiException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiUnavailable);
        }

        if ((form.Scope == EntryScope.Rental && !form.RentalEnabled) || (form.Scope == EntryScope.Flips && !form.FlipsEnabled))
        {
            form.Scope = EntryScope.Personal;
        }

        return PartialView("_EntryForm", form);
    }

    /// <summary>What quick add makes of a line such as "9.80 lunch", for the chips under the box.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The reading, with its names in the person's language.</returns>
    [HttpGet("/entries/quick")]
    public Task<JsonResult> Quick(string? text) => DataAsync(async () =>
    {
        var quick = await Api.EntryQuickAsync(UserId, text ?? string.Empty);
        return new
        {
            quick.Understood,
            quick.Name,
            Amount = quick.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            AmountText = L.Money(quick.Amount),
            Kind = quick.Kind.ToString(),
            KindText = quick.Kind == EntryKind.Inflow ? L.T("Inflow", "Entrada") : L.T("Outflow", "Saída"),
            Scope = quick.Scope.ToString(),
            quick.Category,
            CategoryText = L.Category(quick.Category),
            CategoryColor = Categories.Color(quick.Category),
            quick.AccountId,
            AccountText = quick.AccountName ?? L.T("No account", "Sem conta"),
            Date = quick.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            quick.Remembered,
        };
    });

    /// <summary>Adds an entry, or changes one.</summary>
    /// <param name="form">The fields of the form.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/entries/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] EntryFormFields form)
    {
        if (!Amounts.TryParse(form.Amount, out var amount) || amount <= 0)
        {
            return Invalid("Amount", L.T("The amount must be a number above zero.", "O valor tem de ser um número acima de zero."));
        }

        DateOnly? day = DateOnly.TryParseExact(form.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
        var kind = Enum.TryParse<EntryKind>(form.Kind, ignoreCase: true, out var wanted) ? wanted : (EntryKind?)null;
        var scope = Enum.TryParse<EntryScope>(form.Scope, ignoreCase: true, out var part) ? part : EntryScope.Personal;

        return await AnswerAsync(() => Api.EntrySaveAsync(new EntrySaveRequest
        {
            UserId = UserId,
            Id = form.Id,
            Kind = kind,
            Scope = scope,
            Name = form.Name,
            Amount = amount,
            Date = day,
            Category = kind == EntryKind.Transfer ? null : form.Category,
            AccountId = form.AccountId,
            ToAccountId = kind == EntryKind.Transfer ? form.ToAccountId : null,
            ProjectId = scope == EntryScope.Flips ? form.ProjectId : null,
            ProjectName = scope == EntryScope.Flips && string.IsNullOrWhiteSpace(form.ProjectId) ? form.ProjectName : null,
            Nights = int.TryParse(form.Nights, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nights) ? nights : null,
            Channel = form.Channel,
        }));
    }

    /// <summary>Deletes an entry.</summary>
    /// <param name="id">The entry.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/entries/delete")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Delete([FromForm] string? id) =>
        AnswerAsync(() => Api.EntryDeleteAsync(UserId, id ?? string.Empty), L.T("Entry deleted.", "Entrada apagada."));
}

/// <summary>The fields the entry form posts, as typed.</summary>
public sealed class EntryFormFields
{
    /// <summary>Gets or sets the entry being changed (empty: a new one).</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets "Outflow", "Inflow" or "Transfer".</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets "Personal", "Rental" or "Flips".</summary>
    public string? Scope { get; set; }

    /// <summary>Gets or sets what it was.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the amount, with a comma or a dot.</summary>
    public string? Amount { get; set; }

    /// <summary>Gets or sets the day (yyyy-MM-dd).</summary>
    public string? Date { get; set; }

    /// <summary>Gets or sets the category key.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets the account a transfer goes to.</summary>
    public string? ToAccountId { get; set; }

    /// <summary>Gets or sets the buy-and-sell project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets the name of a new project.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Gets or sets the nights of a booking.</summary>
    public string? Nights { get; set; }

    /// <summary>Gets or sets where a booking came from.</summary>
    public string? Channel { get; set; }
}

/// <summary>
/// The accounts: where the money sits.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
public sealed class AccountsController(LedgerClient apiClient, Lang lang) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>The accounts, with their balances.</summary>
    /// <returns>The page.</returns>
    [HttpGet("/accounts")]
    public Task<IActionResult> Index() =>
        ScreenAsync("Index", Shell(new AccountsPageModel(), AppSection.Accounts), async page => page.Accounts = await Api.AccountListAsync(UserId));

    /// <summary>Adds an account, or changes one.</summary>
    /// <param name="id">The account to change (empty: a new one).</param>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="openingBalance">What was in it before the first entry, as typed.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/accounts/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] string? id, [FromForm] string? name, [FromForm] string? kind, [FromForm] string? openingBalance)
    {
        var opening = 0m;
        var text = (openingBalance ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            if (!Amounts.TryParse(text, out opening))
            {
                return Invalid("OpeningBalance", L.T("The opening balance must be a number.", "O saldo inicial tem de ser um número."));
            }

            if (text.StartsWith('-') || text.StartsWith('−'))
            {
                opening = -opening;
            }
        }

        return await AnswerAsync(() => Api.AccountSaveAsync(new AccountSaveRequest
        {
            UserId = UserId,
            Id = id,
            Name = name,
            Kind = Enum.TryParse<AccountKind>(kind, ignoreCase: true, out var wanted) ? wanted : null,
            OpeningBalance = opening,
        }));
    }

    /// <summary>Deletes an account; its entries stay in the ledger.</summary>
    /// <param name="id">The account.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/accounts/delete")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Delete([FromForm] string? id) =>
        AnswerAsync(() => Api.AccountDeleteAsync(UserId, id ?? string.Empty), L.T("Account deleted. Its entries are still in the ledger.", "Conta apagada. As entradas continuam no livro."));

    /// <summary>Keeps the order the accounts were dragged into.</summary>
    /// <param name="order">The account ids, first to last.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/accounts/order")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Order([FromBody] List<string>? order) =>
        AnswerAsync(() => Api.AccountOrderAsync(new AccountOrderRequest { UserId = UserId, Order = order ?? [] }));
}

/// <summary>
/// The short-term rental module.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
public sealed class RentalController(LedgerClient apiClient, Lang lang) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>A month of the rental. Switched off, it sends the person to Settings.</summary>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The page.</returns>
    [HttpGet("/rental")]
    public async Task<IActionResult> Index(string? month = null)
    {
        if (!LedgerAuthentication.Rental(User))
        {
            return Redirect("/settings#modules");
        }

        return await ScreenAsync("Index", Shell(new RentalPageModel(), AppSection.Rental), async page =>
            page.Data = await Api.RentalGetAsync(UserId, month));
    }
}

/// <summary>
/// The buy-and-sell module.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
public sealed class ProjectsController(LedgerClient apiClient, Lang lang) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>Every project. Switched off, it sends the person to Settings.</summary>
    /// <returns>The page.</returns>
    [HttpGet("/projects")]
    public async Task<IActionResult> Index()
    {
        if (!LedgerAuthentication.Flips(User))
        {
            return Redirect("/settings#modules");
        }

        return await ScreenAsync("Index", Shell(new ProjectsPageModel(), AppSection.Flips), async page =>
            page.Data = await Api.ProjectListAsync(UserId));
    }

    /// <summary>Adds a project, or changes its name, agreed price, target sale price and works budget.</summary>
    /// <param name="id">The project to change (empty: a new one).</param>
    /// <param name="name">The name.</param>
    /// <param name="targetSale">The price hoped for, as typed (empty: none).</param>
    /// <param name="worksBudget">What the works should cost, as typed (empty: none).</param>
    /// <param name="agreedPrice">The price agreed with the seller, as typed (empty: none).</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/projects/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] string? id, [FromForm] string? name, [FromForm] string? targetSale, [FromForm] string? worksBudget, [FromForm] string? agreedPrice)
    {
        if (!Optional(targetSale, out var target) || !Optional(worksBudget, out var budget) || !Optional(agreedPrice, out var agreed))
        {
            return Invalid("TargetSale", L.T("The prices and the budget must be numbers.", "Os preços e o orçamento têm de ser números."));
        }

        return await AnswerAsync(() => Api.ProjectSaveAsync(new ProjectSaveRequest { UserId = UserId, Id = id, Name = name, TargetSale = target, WorksBudget = budget, AgreedPrice = agreed }));

        static bool Optional(string? text, out decimal? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (!Amounts.TryParse(text, out var parsed))
            {
                return false;
            }

            value = parsed;
            return true;
        }
    }

    /// <summary>Deletes a project; its entries stay in the ledger, without a project.</summary>
    /// <param name="id">The project.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/projects/delete")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Delete([FromForm] string? id) =>
        AnswerAsync(() => Api.ProjectDeleteAsync(UserId, id ?? string.Empty), L.T("Project deleted. Its entries are still in the ledger.", "Projeto apagado. As entradas continuam no livro."));
}
