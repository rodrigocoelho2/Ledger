using Ledger.Library.Enums;

namespace Ledger.Library.Categories;

/// <summary>
/// The category a few words most likely belong to, in English or Portuguese: "lunch", "almoço", "Pingo Doce" are food,
/// "fuel", "Via Verde" are transport. Quick add uses it for words it has never seen; once the person saves an entry,
/// what they picked wins the next time.
/// </summary>
public static class CategoryGuess
{
    private static readonly (string Key, EntryKind Kind, string[] Words)[] Rules =
    [
        ("groceries", EntryKind.Outflow, ["groceries", "supermarket", "supermercado", "mercado", "lunch", "almoco", "dinner", "jantar", "breakfast", "pequeno almoco", "coffee", "cafe", "bakery", "padaria", "pastelaria", "restaurant", "restaurante", "takeaway", "uber eats", "glovo", "bolt food", "pingo doce", "continente", "lidl", "aldi", "mercadona", "minipreco", "auchan", "intermarche", "food", "comida", "snack"]),
        ("loans", EntryKind.Outflow, ["mortgage", "loan", "emprestimo", "prestacao"]),
        ("taxes", EntryKind.Outflow, ["tax", "taxes", "imposto", "impostos", "irs", "imi", "iuc", "financas", "seguranca social"]),
        ("insurance", EntryKind.Outflow, ["insurance", "seguro", "seguros", "service charge", "condominio"]),
        ("bills", EntryKind.Outflow, ["electricity", "eletricidade", "luz", "water", "agua", "gas", "internet", "fibre", "fibra", "phone", "telemovel", "mobile", "meo", "vodafone", "digi", "edp", "endesa", "goldenergy", "epal"]),
        ("transport", EntryKind.Outflow, ["fuel", "petrol", "diesel", "gasolina", "gasoleo", "combustivel", "galp", "repsol", "bp", "cepsa", "uber", "bolt", "taxi", "metro", "carris", "cp", "comboio", "train", "bus", "autocarro", "navegante", "toll", "tolls", "portagem", "portagens", "via verde", "parking", "estacionamento", "car wash", "lavagem", "inspecao", "oficina", "mechanic"]),
        ("health", EntryKind.Outflow, ["pharmacy", "farmacia", "doctor", "medico", "consulta", "dentist", "dentista", "hospital", "clinic", "clinica", "physio", "fisioterapia", "optician", "oculista"]),
        ("gym", EntryKind.Outflow, ["gym", "ginasio", "fitness", "crossfit", "boxing", "boxe", "padel", "yoga", "pilates", "protein", "proteina", "supplements", "suplementos", "decathlon"]),
        ("leisure", EntryKind.Outflow, ["cinema", "movies", "netflix", "spotify", "disney", "hbo", "concert", "concerto", "festival", "bar", "beer", "beers", "cerveja", "cervejas", "drinks", "copos", "game", "games", "jogo", "jogos", "steam", "playstation", "nintendo", "xbox", "holiday", "ferias", "trip", "viagem", "hotel", "flight", "voo", "museum", "museu"]),
        ("shopping", EntryKind.Outflow, ["shopping", "compras", "clothes", "roupa", "shoes", "sapatilhas", "sapatos", "zara", "primark", "amazon", "worten", "fnac", "aliexpress", "shein", "gift", "prenda", "presente"]),
        ("home", EntryKind.Outflow, ["rent", "renda", "ikea", "leroy merlin", "furniture", "moveis", "home", "casa"]),
        ("salary", EntryKind.Inflow, ["salary", "salario", "ordenado", "vencimento", "wage", "wages", "payroll"]),
        ("meal-allowance", EntryKind.Inflow, ["meal allowance", "subsidio de refeicao", "subsidio refeicao", "meal card", "cartao refeicao"]),
        ("freelance", EntryKind.Inflow, ["freelance", "invoice", "fatura", "recibo verde", "recibos verdes", "consulting", "consultoria", "side job", "biscate"]),
        ("dividends", EntryKind.Inflow, ["dividend", "dividends", "dividendo", "dividendos", "interest", "juros"]),
        ("refunds", EntryKind.Inflow, ["refund", "reembolso", "devolucao", "cashback"]),
    ];

    /// <summary>The category the words point to, if any.</summary>
    /// <param name="text">What the entry was ("almoço com a equipa").</param>
    /// <param name="kind">Money in or out.</param>
    /// <returns>The category key, or null when nothing fits.</returns>
    public static string? For(string? text, EntryKind kind)
    {
        var words = " " + Words(text) + " ";
        if (words.Trim().Length == 0)
        {
            return null;
        }

        foreach (var (key, ruleKind, list) in Rules)
        {
            if (ruleKind == kind && list.Any(word => words.Contains(" " + word + " ", StringComparison.Ordinal)))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>A meal out (lunch, dinner, coffee): what a meal card usually pays for.</summary>
    /// <param name="text">What the entry was.</param>
    /// <returns>True for a meal.</returns>
    public static bool IsMeal(string? text)
    {
        var words = " " + Words(text) + " ";
        return new[] { "lunch", "almoco", "dinner", "jantar", "breakfast", "pequeno almoco", "coffee", "cafe", "restaurant", "restaurante", "snack", "lanche" }
            .Any(word => words.Contains(" " + word + " ", StringComparison.Ordinal));
    }

    /// <summary>Lower case, without accents, every run of punctuation one space.</summary>
    private static string Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        var space = false;
        foreach (var character in decomposed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                space = false;
            }
            else if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark && !space)
            {
                builder.Append(' ');
                space = true;
            }
        }

        return builder.ToString().Trim();
    }
}
