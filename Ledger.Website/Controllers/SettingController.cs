using System.Text;
using Ledger.Sdk;
using Ledger.Sdk.Models;
using Ledger.Website.Helpers;
using Ledger.Website.Model;
using Ledger.Website.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ledger.Website.Controllers;

/// <summary>
/// Settings: the optional modules, the language, the files (export, import, start over), the password, Discord and,
/// for the admin, the other users.
/// </summary>
/// <param name="apiClient">The Ledger.WebApi client.</param>
/// <param name="lang">The language of the request.</param>
/// <param name="options">Sign-in settings.</param>
public sealed class SettingController(LedgerClient apiClient, Lang lang, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient, lang)
{
    /// <summary>The largest file an import takes, in bytes.</summary>
    public const long MaxImportBytes = 8_000_000;

    private static (DateTime At, List<BankChoiceDTO> List)? banks;

    /// <summary>The settings page.</summary>
    /// <param name="discord">What the Discord step just did (from its redirect).</param>
    /// <returns>The page.</returns>
    [HttpGet("/settings")]
    public Task<IActionResult> Index(string? discord = null)
    {
        var page = Shell(new SettingsPageModel { DiscordEnabled = options.Value.DiscordEnabled, DiscordNote = discord }, AppSection.Settings);
        return ScreenAsync("Index", page, async model =>
        {
            model.Me = await Api.UserGetAsync(UserId);
            model.Users = model.IsAdmin ? await Api.UserListAsync() : [];
            model.DemoMode = (await StatusAsync()).DemoMode;
            model.Bank = await Api.BankStatusAsync(UserId);
            model.Accounts = await Api.AccountListAsync(UserId);
            model.BankPending = Request.Cookies.ContainsKey(BankController.StateCookie);
            if (model.Bank.Enabled)
            {
                (model.Banks, model.BanksProblem) = await BanksAsync();
            }
        });
    }

    /// <summary>The banks of Portugal, kept for half a day (they rarely change, and Enable Banking need not be asked on every visit).</summary>
    private async Task<(List<BankChoiceDTO> Banks, string? Problem)> BanksAsync()
    {
        if (banks is { } kept && DateTime.UtcNow - kept.At < TimeSpan.FromHours(12))
        {
            return (kept.List, null);
        }

        try
        {
            var list = await Api.BankListAsync("PT");
            banks = (DateTime.UtcNow, list);
            return (list, null);
        }
        catch (LedgerApiException exception)
        {
            return ([], string.Join(" ", exception.ToValidationErrors().Select(static error => error.Message).Distinct()));
        }
    }

    /// <summary>Switches the optional modules on or off. Off, a module's tab steps aside; nothing it holds is deleted.</summary>
    /// <param name="rental">Whether the short-term rental module is on.</param>
    /// <param name="flips">Whether the buy-and-sell module is on.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/modules")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Modules([FromForm] bool rental, [FromForm] bool flips) => AnswerAsync(async () =>
    {
        var user = await Api.SettingSaveAsync(new SettingSaveRequest { UserId = UserId, RentalEnabled = rental, FlipsEnabled = flips });
        await LedgerAuthentication.RefreshAsync(HttpContext, user);
    });

    /// <summary>Where the months begin: the calendar, or the payday (and a word of the salary's bank description).</summary>
    /// <param name="payDay">The day of the month the salary is due (empty or 1: calendar months).</param>
    /// <param name="payer">A word of the salary's bank description, such as the employer (empty: the rule alone).</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/month")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> MonthStart([FromForm] int? payDay, [FromForm] string? payer) => AnswerAsync(async () =>
    {
        var user = await Api.SettingSaveAsync(new SettingSaveRequest { UserId = UserId, PayDay = payDay ?? 0, Payer = payer ?? string.Empty });
        await LedgerAuthentication.RefreshAsync(HttpContext, user);
    });

    /// <summary>Picks the language.</summary>
    /// <param name="language">"en" or "pt".</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/language")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Language([FromForm] string? language) => AnswerAsync(async () =>
    {
        var user = await Api.SettingSaveAsync(new SettingSaveRequest { UserId = UserId, Language = language });
        await LedgerAuthentication.RefreshAsync(HttpContext, user);
    });

    /// <summary>Every entry as a CSV file, for a spreadsheet or an accountant.</summary>
    /// <returns>The file.</returns>
    [HttpGet("/settings/export.csv")]
    public Task<IActionResult> ExportCsv() => FileAsync(() => Api.BackupCsvAsync(UserId));

    /// <summary>The whole ledger as a JSON file, to bring back later.</summary>
    /// <returns>The file.</returns>
    [HttpGet("/settings/export.json")]
    public Task<IActionResult> ExportJson() => FileAsync(() => Api.BackupJsonAsync(UserId));

    /// <summary>Reads a CSV or a JSON backup into the ledger.</summary>
    /// <param name="file">The file.</param>
    /// <param name="replace">Whether what is in the ledger is erased first.</param>
    /// <returns>What was added, or the messages to show.</returns>
    [HttpPost("/settings/import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 100_000)]
    public async Task<IActionResult> Import(IFormFile? file, [FromForm] bool replace)
    {
        if (file is null || file.Length == 0)
        {
            return Invalid("File", L.T("Pick a CSV or JSON file first.", "Escolhe primeiro um ficheiro CSV ou JSON."));
        }

        if (file.Length > MaxImportBytes)
        {
            return Invalid("File", L.T("That file is too big (8 MB at most).", "Esse ficheiro é demasiado grande (8 MB no máximo)."));
        }

        string content;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            content = await reader.ReadToEndAsync();
        }

        return await DataAsync(async () =>
        {
            var result = await Api.BackupImportAsync(new ImportRequest { UserId = UserId, Content = content, Replace = replace });
            return new
            {
                Message = L.Count(result.Added, "entry added", "entries added", "entrada adicionada", "entradas adicionadas")
                    + (result.AlreadyThere > 0 ? " · " + L.Count(result.AlreadyThere, "was already there", "were already there", "já lá estava", "já lá estavam") : string.Empty)
                    + (result.AccountsCreated > 0 ? " · " + L.Count(result.AccountsCreated, "account created", "accounts created", "conta criada", "contas criadas") : string.Empty)
                    + (result.ProjectsCreated > 0 ? " · " + L.Count(result.ProjectsCreated, "project created", "projects created", "projeto criado", "projetos criados") : string.Empty)
                    + (result.Skipped > 0 ? " · " + L.Count(result.Skipped, "row couldn't be read", "rows couldn't be read", "linha não foi lida", "linhas não foram lidas") : string.Empty),
            };
        });
    }

    /// <summary>Start over: erases every entry, project, account and cap, and puts the four starting accounts back.</summary>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/reset")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Reset() =>
        AnswerAsync(() => Api.BackupResetAsync(UserId), L.T("Everything was erased. You're back to four empty accounts.", "Foi tudo apagado. Voltaste às quatro contas vazias."));

    /// <summary>Starts linking a Discord account to the signed-in user.</summary>
    /// <returns>A redirect to Discord.</returns>
    [HttpPost("/settings/discord")]
    [ValidateAntiForgeryToken]
    public IActionResult DiscordLink()
    {
        if (!options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/sign-in/discord/done" };
        properties.Items[DiscordAuthentication.ModeKey] = "link";
        return Challenge(properties, DiscordAuthentication.Scheme);
    }

    /// <summary>Unlinks Discord from the signed-in user (refused while it is the only way in).</summary>
    /// <returns>A redirect back to Settings.</returns>
    [HttpPost("/settings/discord/unlink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscordUnlink()
    {
        try
        {
            await Api.UserDiscordLinkAsync(new UserDiscordLinkRequest { Id = UserId });
        }
        catch (LedgerApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            return Redirect("/settings?discord=needs-password#discord");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or LedgerApiException)
        {
            return Redirect("/settings?discord=failed#discord");
        }

        return Redirect("/settings?discord=unlinked#discord");
    }

    /// <summary>Changes the signed-in user's password (signs out every other device).</summary>
    /// <param name="currentPassword">The current password (not asked when the user has none yet).</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/password")]
    [ValidateAntiForgeryToken]
    public Task<JsonResult> Password([FromForm] string? currentPassword, [FromForm] string? newPassword) => AnswerAsync(async () =>
    {
        var user = await Api.UserPasswordAsync(new UserPasswordRequest { Id = UserId, CurrentPassword = currentPassword, NewPassword = newPassword });
        await LedgerAuthentication.SignInAsync(HttpContext, user, true, options.Value.RememberDays);
    }, L.T("Password changed. Your other devices were signed out.", "Palavra-passe alterada. Os teus outros dispositivos foram desligados."));

    /// <summary>Deletes a user and everything they own: the signed-in one, or (admin only) another.</summary>
    /// <param name="id">The user to delete (empty: the signed-in one).</param>
    /// <returns>Fine, or the messages to show.</returns>
    [HttpPost("/settings/delete-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser([FromForm] string? id)
    {
        var target = string.IsNullOrEmpty(id) ? UserId : id;
        if (target != UserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        return await AnswerAsync(async () =>
        {
            await Api.UserDeleteAsync(target);
            if (target == UserId)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        });
    }

    private async Task<IActionResult> FileAsync(Func<Task<ExportDTO>> export)
    {
        try
        {
            var file = await export();
            return File(Encoding.UTF8.GetBytes(file.Content), file.ContentType, file.FileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or LedgerApiException)
        {
            TempData["problem"] = ApiUnavailable;
            return Redirect("/settings#files");
        }
    }
}
