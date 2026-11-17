using System.Globalization;
using System.Security.Claims;
using Ledger.Sdk;
using Ledger.Sdk.Models;
using Ledger.Website.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace Ledger.Website.Security;

/// <summary>
/// Sign-in with an account (user name + password, kept by the WebApi) and a cookie. Every few minutes the cookie is
/// checked against the account, so a deleted account or a changed password ends the other sessions.
/// </summary>
internal static class LedgerAuthentication
{
    /// <summary>The sign-in page.</summary>
    public const string SignInPath = "/sign-in";

    /// <summary>The create-account page.</summary>
    public const string CreateAccountPath = "/create-account";

    /// <summary>The name of the session cookie.</summary>
    public const string CookieName = "ledger.session";

    /// <summary>The claim holding the account's security stamp.</summary>
    public const string StampClaim = "ledger:stamp";

    /// <summary>The claim saying whether the short-term rental module is on.</summary>
    public const string RentalClaim = "ledger:rental";

    /// <summary>The claim saying whether the buy-and-sell module is on.</summary>
    public const string FlipsClaim = "ledger:flips";

    private const string CheckedAtKey = "ledger:checked";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Registers cookie authentication, the sign-in throttle and (optionally) persistent cookie keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLedgerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(WebsiteOptions.SectionName).Get<WebsiteOptions>() ?? new WebsiteOptions();

        var dataProtection = services.AddDataProtection().SetApplicationName("Ledger");
        if (!string.IsNullOrWhiteSpace(options.KeysDirectory))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.KeysDirectory));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();

        var authentication = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // the NAS is usually plain HTTP on the LAN
                cookie.LoginPath = SignInPath;
                cookie.ReturnUrlParameter = "returnUrl";
                cookie.SlidingExpiration = true;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(Math.Clamp(options.RememberDays, 1, 365));
                cookie.Events.OnRedirectToLogin = context =>
                {
                    // Pages go to the sign-in form; the JSON actions get a plain 401.
                    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        // Same-site path only: an absolute http:// address behind an HTTPS proxy would leave the installed app.
                        context.Response.Redirect(Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : context.RedirectUri);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                cookie.Events.OnValidatePrincipal = ValidateAsync;
            });
        if (options.DiscordEnabled)
        {
            authentication.AddLedgerDiscord(options);
        }

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// The principal of a user: who they are, and what they chose in Settings (the language and the optional modules),
    /// so every page knows without asking the WebApi.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The principal stored in the cookie.</returns>
    public static ClaimsPrincipal Principal(UserDTO user) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(StampClaim, user.SecurityStamp),
        new Claim(Lang.Claim, user.Language),
        new Claim(RentalClaim, user.RentalEnabled ? "1" : "0"),
        new Claim(FlipsClaim, user.FlipsEnabled ? "1" : "0"),
    ], CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>Whether the signed-in person switched the short-term rental module on.</summary>
    /// <param name="user">The principal.</param>
    /// <returns>True when it is on.</returns>
    public static bool Rental(ClaimsPrincipal user) => user.FindFirstValue(RentalClaim) == "1";

    /// <summary>Whether the signed-in person switched the buy-and-sell module on.</summary>
    /// <param name="user">The principal.</param>
    /// <returns>True when it is on.</returns>
    public static bool Flips(ClaimsPrincipal user) => user.FindFirstValue(FlipsClaim) == "1";

    /// <summary>
    /// Signs the browser in again as the same user, keeping how long the session lasts (after Settings changed).
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="user">The user as it is now.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    public static async Task RefreshAsync(HttpContext context, UserDTO user)
    {
        var current = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = current.Properties ?? new AuthenticationProperties { AllowRefresh = true };
        properties.Items[CheckedAtKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(user), properties);
        Remember(context, user.Language);
    }

    /// <summary>Remembers the language on this device, for the pages before signing in.</summary>
    /// <param name="context">The request.</param>
    /// <param name="language">"en" or "pt".</param>
    public static void Remember(HttpContext context, string language) =>
        context.Response.Cookies.Append(Lang.CookieName, language, new CookieOptions
        {
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            IsEssential = true,
        });

    /// <summary>
    /// Signs the browser in as an account.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="user">The account.</param>
    /// <param name="remember">Keep the session after the browser closes.</param>
    /// <param name="rememberDays">For how long, when remembered.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    public static Task SignInAsync(HttpContext context, UserDTO user, bool remember, int rememberDays)
    {
        var properties = new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(Math.Clamp(rememberDays, 1, 365)) : null,
            AllowRefresh = true,
        };
        properties.Items[CheckedAtKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        Remember(context, user.Language);
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(user), properties);
    }

    /// <summary>The account id of the signed-in user, or empty.</summary>
    /// <param name="user">The principal.</param>
    /// <returns>The id.</returns>
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// Every <see cref="CheckEvery"/>, makes sure the account still exists and its password did not change.
    /// When the WebApi cannot be reached, the session is kept (the pages show the connection problem instead).
    /// </summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.Properties.Items.TryGetValue(CheckedAtKey, out var checkedAt)
            && long.TryParse(checkedAt, CultureInfo.InvariantCulture, out var seconds)
            && now - DateTimeOffset.FromUnixTimeSeconds(seconds) < CheckEvery)
        {
            return;
        }

        var id = context.Principal is null ? string.Empty : UserId(context.Principal);
        var stamp = context.Principal?.FindFirstValue(StampClaim) ?? string.Empty;
        var client = context.HttpContext.RequestServices.GetRequiredService<LedgerClient>();
        try
        {
            var user = await client.UserGetAsync(id);
            if (user.SecurityStamp != stamp)
            {
                await RejectAsync(context);
                return;
            }

            context.ReplacePrincipal(Principal(user));
        }
        catch (LedgerApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await RejectAsync(context);
            return;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or LedgerApiException)
        {
            return;   // the WebApi is down: keep the session, check again on the next request
        }

        context.Properties.Items[CheckedAtKey] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
