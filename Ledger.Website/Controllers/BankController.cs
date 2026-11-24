using Ledger.Sdk;
using Ledger.Sdk.Models;
using Ledger.Website.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Ledger.Website.Controllers;

/// <summary>
/// Bank connections (Settings → Bank): send the person to their bank through Enable Banking, take them back, and the
/// buttons of each connected account.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
public sealed class BankController(LedgerClient apiClient, Lang lang) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>The cookie that remembers the authorisation under way (its state), for the callback or a paste.</summary>
    public const string StateCookie = "ledger-bank";

    /// <summary>Where the bank sends the person back.</summary>
    public const string CallbackPath = "/bank/callback";

    /// <summary>Starts connecting a bank: answers with the bank's address, and whether the person will paste where they land.</summary>
    /// <param name="bank">The bank's name.</param>
    /// <returns>Where to go, or the messages to show.</returns>
    [HttpPost("/bank/start")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Start([FromForm] string? bank) => DataAsync(async () =>
    {
        var start = await Api.BankStartAsync(new BankStartRequest
        {
            UserId = UserId,
            Bank = bank,
            Country = "PT",
            RedirectUrl = $"{Request.Scheme}://{Request.Host}{CallbackPath}",
        });
        Response.Cookies.Append(StateCookie, start.State, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1),
            Path = "/",
        });
        return new { start.Url, start.Paste };
    });

    /// <summary>The bank sends the person back here (when this address is registered at Enable Banking).</summary>
    /// <param name="code">The authorisation code.</param>
    /// <param name="state">The state sent at the start.</param>
    /// <param name="error">The bank's error, if the person said no.</param>
    /// <param name="error_description">The bank's words for it.</param>
    /// <returns>Back to Settings.</returns>
    [HttpGet(CallbackPath)]
#pragma warning disable IDE1006, SA1313 // The names the bank uses in the address.
    public async Task<IActionResult> Callback(string? code, string? state, string? error, string? error_description)
#pragma warning restore IDE1006, SA1313
    {
        if (error is not null || string.IsNullOrEmpty(code))
        {
            TempData["problem"] = L.T("The bank didn't authorise the connection", "O banco não autorizou a ligação") + (error_description ?? error) switch
            {
                null => ".",
                var why => $": {why}",
            };
            return Redirect("/settings#bank");
        }

        return await FinishAsync(code, state ?? Request.Cookies[StateCookie]);
    }

    /// <summary>Finishes with the address the person pasted (when the bank sent them somewhere else).</summary>
    /// <param name="address">The address they landed on.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/bank/paste")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Paste([FromForm] string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Task.FromResult(Invalid("Code", L.T("Paste the whole address you landed on.", "Cola o endereço completo onde foste parar.")));
        }

        return AnswerAsync(async () =>
        {
            await Api.BankCompleteAsync(new BankCompleteRequest { UserId = UserId, Code = address.Trim(), State = Request.Cookies[StateCookie] });
            Response.Cookies.Delete(StateCookie);
        }, L.T("Bank connected. Now pick the account its movements go into.", "Banco ligado. Agora escolhe a conta para onde vão os movimentos."));
    }

    /// <summary>Forgets an authorisation that didn't finish.</summary>
    /// <returns>Fine.</returns>
    [HttpPost("/bank/cancel")]
    [ValidateAntiForgeryToken]
    public JsonResult Cancel()
    {
        Response.Cookies.Delete(StateCookie);
        return Json(new Model.AjaxResult());
    }

    /// <summary>Says which account a bank account goes into, and brings its movements.</summary>
    /// <param name="id">The connection.</param>
    /// <param name="account">The account id, "new", or empty to stop.</param>
    /// <param name="from">The first day to bring (yyyy-MM-dd).</param>
    /// <returns>What came, or the messages to show.</returns>
    [HttpPost("/bank/assign")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Assign([FromForm] string? id, [FromForm] string? account, [FromForm] DateOnly? from) => DataAsync(async () =>
    {
        var result = await Api.BankAssignAsync(new BankAssignRequest { UserId = UserId, LinkId = id, AccountId = account, ImportFrom = from });
        return new { Message = Synced(result) };
    });

    /// <summary>Brings a connected account's latest movements now.</summary>
    /// <param name="id">The connection.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/bank/sync")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Sync([FromForm] string? id) => AnswerAsync(async () =>
    {
        var result = await Api.BankSyncAsync(UserId, id ?? string.Empty);
        TempData["notice"] = Synced(result);
    });

    /// <summary>Makes Ledger's balance agree with the bank's (the opening balance changes).</summary>
    /// <param name="id">The connection.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/bank/align")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Align([FromForm] string? id) =>
        AnswerAsync(() => Api.BankAlignAsync(new BankAlignRequest { UserId = UserId, LinkId = id }), L.T("The balance now agrees with the bank.", "O saldo agora bate certo com o banco."));

    /// <summary>Disconnects a bank account; what it brought stays.</summary>
    /// <param name="id">The connection.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/bank/delete")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Delete([FromForm] string? id) =>
        AnswerAsync(() => Api.BankDeleteAsync(UserId, id ?? string.Empty), L.T("Bank account disconnected. Its movements stay in the ledger.", "Conta bancária desligada. Os movimentos continuam no livro."));

    /// <summary>Marks the bank's movements as looked at: the ones given, or all.</summary>
    /// <param name="id">The entries, separated by commas (none: every one).</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/entries/reviewed")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Reviewed([FromForm] string? id) =>
        AnswerAsync(() => Api.EntryReviewedAsync(new EntryReviewedRequest
        {
            UserId = UserId,
            Ids = [.. (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        }), L.T("Marked as seen.", "Marcado como visto."));

    private async Task<IActionResult> FinishAsync(string code, string? state)
    {
        try
        {
            await Api.BankCompleteAsync(new BankCompleteRequest { UserId = UserId, Code = code, State = state });
            Response.Cookies.Delete(StateCookie);
            TempData["notice"] = L.T("Bank connected. Now pick the account its movements go into.", "Banco ligado. Agora escolhe a conta para onde vão os movimentos.");
        }
        catch (LedgerApiException exception)
        {
            TempData["problem"] = string.Join(" ", exception.ToValidationErrors().Select(static error => error.Message).Distinct());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            TempData["problem"] = ApiUnavailable;
        }

        return Redirect("/settings#bank");
    }

    private string Synced(BankSyncDTO result) =>
        (result.Imported, result.Matched, result.Transfers) switch
        {
            (0, 0, 0) => L.T("Up to date: nothing new at the bank.", "Em dia: nada de novo no banco."),
            var (imported, matched, transfers) => string.Join(" · ", new[]
            {
                imported > 0 ? L.Count(imported, "movement brought, to review", "movements brought, to review", "movimento trazido, por rever", "movimentos trazidos, por rever") : null,
                matched > 0 ? L.Count(matched, "of yours confirmed by the bank", "of yours confirmed by the bank", "dos teus confirmado pelo banco", "dos teus confirmados pelo banco") : null,
                transfers > 0 ? L.Count(transfers, "transfer between your accounts", "transfers between your accounts", "transferência entre as tuas contas", "transferências entre as tuas contas") : null,
            }.Where(static part => part is not null)),
        };
}
