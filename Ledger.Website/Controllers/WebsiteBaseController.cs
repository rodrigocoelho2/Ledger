using Ledger.Library.Validation;
using Ledger.Sdk;
using Ledger.Website.Helpers;
using Ledger.Website.Model;
using Ledger.Website.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ledger.Website.Controllers;

/// <summary>
/// Base of every signed-in Website controller: fills in the shell of a page, reads its data from the WebApi, and turns
/// "the engine isn't answering" into a card instead of an error page.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
[Microsoft.AspNetCore.Authorization.Authorize]
public abstract class WebsiteBaseController(LedgerClient apiClient, Lang lang) : Controller
{
    private const string NoticeKey = "notice";
    private const string ProblemKey = "problem";

    /// <summary>Gets the API client.</summary>
    protected LedgerClient Api { get; } = apiClient;

    /// <summary>Gets the language of the request.</summary>
    protected Lang L { get; } = lang;

    /// <summary>Gets the signed-in user's id.</summary>
    protected string UserId => LedgerAuthentication.UserId(User);

    /// <summary>Gets what the pages say when the WebApi can't be reached.</summary>
    protected string ApiUnavailable => L.T(
        "Can't reach Ledger's engine (WebApi) right now. Check that the ledger-webapi container is running.",
        "Não consigo chegar ao motor do Ledger (WebApi). Confirma que o contentor ledger-webapi está a correr.");

    /// <summary>
    /// Fills in the shell of a page: who's signed in, the tab, the modules and the message a form left.
    /// </summary>
    /// <typeparam name="T">The page model.</typeparam>
    /// <param name="page">The page.</param>
    /// <param name="section">The tab.</param>
    /// <returns>The page.</returns>
    protected T Shell<T>(T page, AppSection section)
        where T : AppPageModel
    {
        page.UserName = User.Identity?.Name ?? string.Empty;
        page.IsAdmin = User.IsInRole("Admin");
        page.Section = section;
        page.RentalEnabled = LedgerAuthentication.Rental(User);
        page.FlipsEnabled = LedgerAuthentication.Flips(User);
        page.Notice = TempData[NoticeKey] as string;
        page.Problem = TempData[ProblemKey] as string;
        return page;
    }

    /// <summary>
    /// Reads a page's data and shows it, or the "engine isn't answering" card.
    /// </summary>
    /// <typeparam name="T">The page model.</typeparam>
    /// <param name="viewName">The screen's view.</param>
    /// <param name="page">The page (shell filled in).</param>
    /// <param name="load">Reads the data into the page.</param>
    /// <returns>The view.</returns>
    protected async Task<IActionResult> ScreenAsync<T>(string viewName, T page, Func<T, Task> load)
        where T : AppPageModel
    {
        try
        {
            await load(page);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or LedgerApiException)
        {
            page.Unavailable = exception is LedgerApiException api && api.StatusCode != System.Net.HttpStatusCode.Unauthorized
                ? api.Message
                : ApiUnavailable;
        }

        return View(page.Unavailable is null ? viewName : "~/Views/Shared/Unavailable.cshtml", page);
    }

    /// <summary>
    /// Runs what a page script asked for and answers with JSON: fine, or the messages to show.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="notice">What the next page says when it worked (the script reloads on success).</param>
    /// <returns>The answer.</returns>
    protected async Task<JsonResult> AnswerAsync(Func<Task> call, string? notice = null)
    {
        try
        {
            await call();
            if (notice is not null)
            {
                TempData[NoticeKey] = notice;
            }

            return Json(new AjaxResult());
        }
        catch (LedgerApiException exception)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, ApiUnavailable)] });
        }
    }

    /// <summary>
    /// Calls the API for a page script and wraps the answer.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="call">The call.</param>
    /// <returns>The data, or the messages to show.</returns>
    protected async Task<JsonResult> DataAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return Json(new AjaxDataResult<T> { Data = await call() });
        }
        catch (LedgerApiException exception)
        {
            return Json(new AjaxDataResult<T> { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Json(new AjaxDataResult<T> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, ApiUnavailable)] });
        }
    }

    /// <summary>A JSON answer that says one thing went wrong.</summary>
    /// <param name="field">The field it is about.</param>
    /// <param name="message">The message.</param>
    /// <returns>The answer.</returns>
    protected JsonResult Invalid(string field, string message) =>
        Json(new AjaxResult { IsValid = false, ValidationErrors = [new ApiValidationError(field, message)] });
}
