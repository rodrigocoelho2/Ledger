using System.Globalization;
using System.Security.Claims;
using Ledger.Library.Categories;
using Ledger.Library.Enums;

namespace Ledger.Website.Helpers;

/// <summary>
/// The language of the request, and everything that depends on it: the words, the money, the dates. Ledger speaks
/// English and Portuguese, so every text is written once in each, side by side, where it is used:
/// <c>L.T("Accounts", "Contas")</c>. Signed in, the language is the one picked in Settings; signed out, the last one
/// used on this device, or else the browser's.
/// </summary>
public sealed class Lang
{
    /// <summary>The cookie that remembers the language on this device (for the pages before signing in).</summary>
    public const string CookieName = "ledger.lang";

    /// <summary>The claim holding the language picked in Settings.</summary>
    public const string Claim = "ledger:lang";

    private const char Space = ' ';

    private static readonly string[] EnglishMonths = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] PortugueseMonths = ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"];
    private static readonly string[] EnglishDays = ["M", "T", "W", "T", "F", "S", "S"];
    private static readonly string[] PortugueseDays = ["S", "T", "Q", "Q", "S", "S", "D"];

    /// <summary>
    /// Reads the language of the current request.
    /// </summary>
    /// <param name="httpContextAccessor">The request.</param>
    public Lang(IHttpContextAccessor httpContextAccessor)
    {
        Code = Resolve(httpContextAccessor.HttpContext);
        Pt = Code == Languages.Portuguese;
    }

    /// <summary>Gets the language: "en" or "pt".</summary>
    public string Code { get; }

    /// <summary>Gets a value indicating whether the language is Portuguese.</summary>
    public bool Pt { get; }

    /// <summary>
    /// The language of a request: the signed-in person's choice, else this device's cookie, else the browser's.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>"en" or "pt".</returns>
    public static string Resolve(HttpContext? context)
    {
        if (context is null)
        {
            return Languages.English;
        }

        if (context.User.FindFirstValue(Claim) is { Length: > 0 } chosen)
        {
            return Languages.Normalize(chosen);
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var remembered) && !string.IsNullOrEmpty(remembered))
        {
            return Languages.Normalize(remembered);
        }

        // The browser's first language decides ("pt-PT,pt;q=0.9,en;q=0.8" is Portuguese).
        return Languages.Normalize(context.Request.Headers.AcceptLanguage.ToString().TrimStart());
    }

    /// <summary>Picks the text in the request's language.</summary>
    /// <param name="english">The English text.</param>
    /// <param name="portuguese">The Portuguese text.</param>
    /// <returns>One of the two.</returns>
    public string T(string english, string portuguese) => Pt ? portuguese : english;

    /// <summary>An amount of money: "€2,154.90" in English, "2 154,90 €" in Portuguese. Negative amounts get a real minus sign.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="cents">Whether the cents are written.</param>
    /// <returns>The text.</returns>
    public string Money(decimal amount, bool cents = true)
    {
        var number = Number(Math.Abs(amount), cents ? 2 : 0);
        var text = Pt ? number + Space + "€" : "€" + number;
        return amount < 0 ? "−" + text : text;
    }

    /// <summary>An amount with its sign always written: "+€827.16", "−€118.00".</summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The text.</returns>
    public string Signed(decimal amount) => amount > 0 ? "+" + Money(amount) : Money(amount);

    /// <summary>A number with the language's separators.</summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">How many decimals.</param>
    /// <returns>The text.</returns>
    public string Number(decimal value, int decimals = 0)
    {
        var invariant = decimal.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("N" + decimals, CultureInfo.InvariantCulture);
        return Pt ? invariant.Replace(',', '\u0001').Replace('.', ',').Replace('\u0001', Space) : invariant;
    }

    /// <summary>A share (0 to 1) as a percentage: "38%", "17.0%".</summary>
    /// <param name="share">The share.</param>
    /// <param name="decimals">How many decimals.</param>
    /// <returns>The text.</returns>
    public string Percent(decimal share, int decimals = 0)
    {
        var number = Number(Math.Abs(share) * 100m, decimals);
        return (share < 0 ? "−" : string.Empty) + number + "%";
    }

    /// <summary>An amount for a text box: no currency sign, no thousands separators ("1960,00" / "1960.00").</summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The text.</returns>
    public string Input(decimal? amount) =>
        amount is { } value ? value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', Pt ? ',' : '.') : string.Empty;

    /// <summary>The name of a month: "October" / "Outubro".</summary>
    /// <param name="day">Any day of it.</param>
    /// <returns>The name.</returns>
    public string Month(DateOnly day) => (Pt ? PortugueseMonths : EnglishMonths)[day.Month - 1];

    /// <summary>The short name of a month: "Oct" / "Out".</summary>
    /// <param name="day">Any day of it.</param>
    /// <returns>The first three letters.</returns>
    public string MonthShort(DateOnly day) => Month(day)[..3];

    /// <summary>A day without its year: "30 Oct" / "30 out".</summary>
    /// <param name="day">The day.</param>
    /// <returns>The text.</returns>
    public string Day(DateOnly day) => day.Day.ToString(CultureInfo.InvariantCulture) + " " + (Pt ? MonthShort(day).ToLowerInvariant() : MonthShort(day));

    /// <summary>A day with its year: "12 Oct 2026" / "12 out 2026".</summary>
    /// <param name="day">The day.</param>
    /// <returns>The text.</returns>
    public string FullDay(DateOnly day) => Day(day) + " " + day.Year.ToString(CultureInfo.InvariantCulture);

    /// <summary>Two days as a stretch: "3 – 11 Oct", or "30 Sep – 3 Oct" across months.</summary>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <returns>The text.</returns>
    public string Stretch(DateOnly from, DateOnly to) =>
        from.Month == to.Month && from.Year == to.Year
            ? from.Day.ToString(CultureInfo.InvariantCulture) + " – " + Day(to)
            : Day(from) + " – " + Day(to);

    /// <summary>The first letter of a weekday, Monday first.</summary>
    /// <param name="day">The day.</param>
    /// <returns>One letter.</returns>
    public string Weekday(DateOnly day) => (Pt ? PortugueseDays : EnglishDays)[((int)day.DayOfWeek + 6) % 7];

    /// <summary>The name of a category (a transfer has none: "Transfer").</summary>
    /// <param name="key">The category key.</param>
    /// <returns>The name.</returns>
    public string Category(string? key) => key is null ? T("Transfer", "Transferência") : Categories.Name(key, Code);

    /// <summary>The name of a kind of account.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public string Kind(AccountKind kind) => kind switch
    {
        AccountKind.Savings => T("Savings", "Poupança"),
        AccountKind.Investments => T("Investments", "Investimentos"),
        AccountKind.MealCard => T("Meal card", "Cartão refeição"),
        _ => T("Current account", "Conta à ordem"),
    };

    /// <summary>The name of a part of life.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The name.</returns>
    public string Scope(EntryScope scope) => scope switch
    {
        EntryScope.Rental => T("Short-term rental", "Alojamento local"),
        EntryScope.Flips => T("Buy & Sell", "Compra & Venda"),
        _ => T("Personal", "Pessoal"),
    };

    /// <summary>The first day of a month written as "2026-10".</summary>
    /// <param name="month">The month (yyyy-MM).</param>
    /// <returns>The first day; today's month when the text isn't one.</returns>
    public static DateOnly MonthStart(string? month) =>
        DateOnly.TryParseExact((month ?? string.Empty) + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : new DateOnly(Clock.Today.Year, Clock.Today.Month, 1);

    /// <summary>What an entry did to the money, with its sign: "+€1,850.00", "−€9.80", or bare for a transfer.</summary>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="amount">The amount (always positive).</param>
    /// <returns>The text.</returns>
    public string Entry(EntryKind kind, decimal amount) => kind switch
    {
        EntryKind.Inflow => "+" + Money(amount),
        EntryKind.Outflow => "−" + Money(amount),
        _ => Money(amount),
    };

    /// <summary>The CSS class of an entry's amount.</summary>
    /// <param name="kind">The kind of entry.</param>
    /// <returns>"in", "outflow" or "move".</returns>
    public static string Tone(EntryKind kind) => kind switch
    {
        EntryKind.Inflow => "in",
        EntryKind.Outflow => "outflow",
        _ => "move",
    };

    /// <summary>The CSS class of an account's kind.</summary>
    /// <param name="kind">The kind of account.</param>
    /// <returns>"kind-current", "kind-savings"…</returns>
    public static string Tick(AccountKind kind) => "kind-" + kind.ToString().ToLowerInvariant();

    /// <summary>How long ago something happened: "just now", "12 min ago", "3 h ago", "yesterday", "4 days ago".</summary>
    /// <param name="when">When it happened.</param>
    /// <returns>The text.</returns>
    public string Ago(DateTimeOffset when)
    {
        var gone = DateTimeOffset.UtcNow - when;
        var minutes = (int)Math.Max(0, gone.TotalMinutes);
        return minutes switch
        {
            < 2 => T("just now", "agora mesmo"),
            < 60 => T($"{minutes} min ago", $"há {minutes} min"),
            < 60 * 24 => T($"{minutes / 60} h ago", $"há {minutes / 60} h"),
            < 60 * 48 => T("yesterday", "ontem"),
            _ => T($"{minutes / (60 * 24)} days ago", $"há {minutes / (60 * 24)} dias"),
        };
    }

    /// <summary>A count with its noun: "1 entry", "29 entries".</summary>
    /// <param name="count">The count.</param>
    /// <param name="englishOne">The English singular.</param>
    /// <param name="englishMany">The English plural.</param>
    /// <param name="portugueseOne">The Portuguese singular.</param>
    /// <param name="portugueseMany">The Portuguese plural.</param>
    /// <returns>The text.</returns>
    public string Count(int count, string englishOne, string englishMany, string portugueseOne, string portugueseMany) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? T(englishOne, portugueseOne) : T(englishMany, portugueseMany));
}

/// <summary>
/// Sends the person's language to the WebApi with every call, so its validation messages come back in it.
/// </summary>
/// <param name="httpContextAccessor">The request being served.</param>
internal sealed class LanguageHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd(Lang.Resolve(httpContextAccessor.HttpContext));
        return base.SendAsync(request, cancellationToken);
    }
}
