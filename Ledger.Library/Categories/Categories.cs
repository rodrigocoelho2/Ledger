using Ledger.Library.Enums;

namespace Ledger.Library.Categories;

/// <summary>
/// One category of the fixed list: what an entry was for.
/// </summary>
/// <param name="Key">The key stored with the entry (never changes).</param>
/// <param name="Kind">Whether it names an outflow or an inflow.</param>
/// <param name="Scopes">The parts of life it is offered in.</param>
/// <param name="English">Its English name.</param>
/// <param name="Portuguese">Its Portuguese name.</param>
/// <param name="Color">Its colour in the charts (#RRGGBB).</param>
public sealed record Category(string Key, EntryKind Kind, EntryScope[] Scopes, string English, string Portuguese, string Color)
{
    /// <summary>The name in a language.</summary>
    /// <param name="language">"pt" for Portuguese; anything else is English.</param>
    /// <returns>The name.</returns>
    public string Name(string? language) => Languages.IsPortuguese(language) ? Portuguese : English;

    /// <summary>Whether the category is offered in a part of life.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>True when it is.</returns>
    public bool In(EntryScope scope) => Array.IndexOf(Scopes, scope) >= 0;
}

/// <summary>
/// The languages the app speaks.
/// </summary>
public static class Languages
{
    /// <summary>English (the default).</summary>
    public const string English = "en";

    /// <summary>Portuguese (Portugal).</summary>
    public const string Portuguese = "pt";

    /// <summary>Whether a language code means Portuguese.</summary>
    /// <param name="language">The code ("pt", "pt-PT"…).</param>
    /// <returns>True for Portuguese.</returns>
    public static bool IsPortuguese(string? language) =>
        language is not null && language.StartsWith(Portuguese, StringComparison.OrdinalIgnoreCase);

    /// <summary>The language to use for a code: Portuguese, or English for anything else.</summary>
    /// <param name="language">The code.</param>
    /// <returns>"pt" or "en".</returns>
    public static string Normalize(string? language) => IsPortuguese(language) ? Portuguese : English;
}

/// <summary>
/// The fixed list of categories. Keys are stored with the entries, so a key is never renamed or reused; the names and
/// colours can change freely.
/// </summary>
public static class Categories
{
    /// <summary>The key of loan repayments: money that went into a project without being one of its costs.</summary>
    public const string Loans = "loans";

    /// <summary>The key of the inflow that closes a buy-and-sell project.</summary>
    public const string Sale = "sale";

    /// <summary>The key of a rental booking.</summary>
    public const string Bookings = "bookings";

    /// <summary>The key of the property's price (deposit and the rest at the deed).</summary>
    public const string Purchase = "purchase";

    /// <summary>The key of the taxes and the deed.</summary>
    public const string Deed = "deed";

    /// <summary>The key of works and materials.</summary>
    public const string Works = "works";

    /// <summary>The key of labour.</summary>
    public const string Labour = "labour";

    /// <summary>The key outflows fall back to.</summary>
    public const string Other = "other";

    /// <summary>The key inflows fall back to.</summary>
    public const string OtherIncome = "other-income";

    /// <summary>The colour of a transfer, which has no category.</summary>
    public const string TransferColor = "#8B8694";

    private static readonly EntryScope[] Personal = [EntryScope.Personal];
    private static readonly EntryScope[] Rental = [EntryScope.Rental];
    private static readonly EntryScope[] Flips = [EntryScope.Flips];
    private static readonly EntryScope[] PersonalRental = [EntryScope.Personal, EntryScope.Rental];
    private static readonly EntryScope[] Everywhere = [EntryScope.Personal, EntryScope.Rental, EntryScope.Flips];

    private static readonly Category[] List =
    [
        // Outflows of everyday life
        new("groceries", EntryKind.Outflow, Personal, "Groceries & food", "Alimentação", "#E39468"),
        new("home", EntryKind.Outflow, Personal, "Home & rent", "Casa & renda", "#F0B7A4"),
        new("bills", EntryKind.Outflow, PersonalRental, "Bills & utilities", "Contas & serviços", "#8FA9D9"),
        new("transport", EntryKind.Outflow, Personal, "Transport", "Transportes", "#7DBFCF"),
        new("leisure", EntryKind.Outflow, Personal, "Leisure", "Lazer", "#D9A8E0"),
        new("health", EntryKind.Outflow, Personal, "Health", "Saúde", "#E8D08A"),
        new("gym", EntryKind.Outflow, Personal, "Gym & fitness", "Ginásio & desporto", "#A8C47F"),
        new("shopping", EntryKind.Outflow, Personal, "Shopping", "Compras", "#C2B5F0"),
        new(Loans, EntryKind.Outflow, Everywhere, "Loans & repayments", "Crédito & prestações", "#D9788F"),
        new("taxes", EntryKind.Outflow, PersonalRental, "Taxes & duties", "Impostos & taxas", "#C9A27C"),
        new("insurance", EntryKind.Outflow, PersonalRental, "Service charge & insurance", "Condomínio & seguros", "#B9B2A3"),

        // Outflows of a place rented by the night
        new("cleaning", EntryKind.Outflow, Rental, "Cleaning", "Limpeza", "#D6B072"),
        new("platform-fees", EntryKind.Outflow, Rental, "Platform fees", "Comissões de plataforma", "#E39468"),
        new("guest-supplies", EntryKind.Outflow, Rental, "Guest supplies", "Consumíveis para hóspedes", "#E8D08A"),
        new("maintenance", EntryKind.Outflow, Rental, "Maintenance", "Manutenção", "#D9A8E0"),

        // Outflows of a property bought to sell
        new(Purchase, EntryKind.Outflow, Flips, "Purchase", "Aquisição", "#B99ACF"),
        new(Deed, EntryKind.Outflow, Flips, "Transfer tax & deed", "IMT, IS & escritura", "#D9788F"),
        new(Works, EntryKind.Outflow, Flips, "Works & materials", "Obras & materiais", "#E39468"),
        new(Labour, EntryKind.Outflow, Flips, "Labour", "Mão de obra", "#E8D08A"),
        new("permits", EntryKind.Outflow, Flips, "Permits & fees", "Licenças & taxas", "#B9B2A3"),
        new("agency", EntryKind.Outflow, Flips, "Agency & sale costs", "Mediação & venda", "#8FA9D9"),
        new("capital-gains", EntryKind.Outflow, Flips, "Capital gains tax", "Mais-valias", "#C9A27C"),

        new(Other, EntryKind.Outflow, Everywhere, "Other", "Outros", "#8B8694"),

        // Inflows
        new("salary", EntryKind.Inflow, Personal, "Salary", "Salário", "#7FC7AE"),
        new("meal-allowance", EntryKind.Inflow, Personal, "Meal allowance", "Subsídio de refeição", "#A6D9C6"),
        new("freelance", EntryKind.Inflow, Personal, "Freelance", "Trabalho independente", "#8FD3DD"),
        new("dividends", EntryKind.Inflow, Personal, "Dividends & interest", "Dividendos & juros", "#C9E2A8"),
        new(Bookings, EntryKind.Inflow, Rental, "Bookings", "Reservas", "#D6B072"),
        new(Sale, EntryKind.Inflow, Flips, "Property sale", "Venda do imóvel", "#7FC7AE"),
        new("refunds", EntryKind.Inflow, Everywhere, "Refunds", "Reembolsos", "#9CC3E6"),
        new(OtherIncome, EntryKind.Inflow, Everywhere, "Other income", "Outras entradas", "#8B8694"),
    ];

    private static readonly Dictionary<string, Category> ByKey = List.ToDictionary(static category => category.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets every category, in the order the pickers show them.</summary>
    public static IReadOnlyList<Category> All => List;

    /// <summary>Finds a category by its key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The category, or null.</returns>
    public static Category? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var category) ? category : null;

    /// <summary>The categories offered for a kind of entry in a part of life.</summary>
    /// <param name="kind">Outflow or inflow (a transfer has none).</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The categories.</returns>
    public static IReadOnlyList<Category> For(EntryKind kind, EntryScope scope) =>
        [.. List.Where(category => category.Kind == kind && category.In(scope))];

    /// <summary>The category an entry falls back to when it has none that fits.</summary>
    /// <param name="kind">Outflow or inflow.</param>
    /// <returns>"other" or "other-income".</returns>
    public static string Fallback(EntryKind kind) => kind == EntryKind.Inflow ? OtherIncome : Other;

    /// <summary>The name of a category in a language (the key itself when it isn't in the list).</summary>
    /// <param name="key">The key.</param>
    /// <param name="language">The language.</param>
    /// <returns>The name.</returns>
    public static string Name(string? key, string? language) => Find(key)?.Name(language) ?? key ?? string.Empty;

    /// <summary>The colour of a category (the transfer grey when it isn't in the list).</summary>
    /// <param name="key">The key.</param>
    /// <returns>The colour.</returns>
    public static string Color(string? key) => Find(key)?.Color ?? TransferColor;

    /// <summary>
    /// Finds a category by its key or by its name in either language, ignoring case, accents and punctuation: how a
    /// CSV written by hand names them.
    /// </summary>
    /// <param name="text">The key or the name.</param>
    /// <param name="kind">The kind the category must have.</param>
    /// <returns>The category, or null.</returns>
    public static Category? Match(string? text, EntryKind kind)
    {
        var wanted = Simplify(text);
        if (wanted.Length == 0)
        {
            return null;
        }

        return List.FirstOrDefault(category => category.Kind == kind
            && (Simplify(category.Key) == wanted || Simplify(category.English) == wanted || Simplify(category.Portuguese) == wanted));
    }

    /// <summary>Lower case, without accents, spaces or punctuation.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The simplified text.</returns>
    public static string Simplify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
