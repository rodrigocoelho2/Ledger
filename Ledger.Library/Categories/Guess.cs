using Ledger.Library.Enums;

namespace Ledger.Library.Categories;

/// <summary>
/// The category a few words most likely belong to, in English or Portuguese: "lunch", "almoço", "Pingo Doce" are food,
/// "fuel", "Via Verde" are transport. Quick add uses it for words it has never seen; once the person saves an entry,
/// what they picked wins the next time.
/// </summary>
public static class CategoryGuess
{
    // Shops and brands, as a bank writes them: matched at the start of a word, because card purchases glue the town on
    // ("Pingo Docelisboa", "PLAYSTATIONHilversum"). Checked before the plain words, and only names long enough to be safe.
    private static readonly (string Key, EntryKind Kind, string[] Names)[] Merchants =
    [
        ("groceries", EntryKind.Outflow, ["pingo doce", "continente", "minipreco", "lidl", "aldi", "mercadona", "auchan", "intermarche", "express mini mercado", "poupar mais", "meu super", "spar "]),
        (Categories.EatingOut, EntryKind.Outflow, ["mcdonald", "burger king", "kfc ", "domino", "pizza hut", "telepizza", "pizza divina", "kebab", "starbucks", "pad port", "padaria portuguesa", "real cake", "leitaria", "pastelaria", "h3 ", "oakberry", "sakura", "sabor mineiro", "o carvoeiro", "espiga dourada", "zin vending", "vending", "uber eats", "glovo", "bolt food", "taco bell", "five guys", "vitaminas", "padaria"]),
        ("gym", EntryKind.Outflow, ["prozis", "boulevard legend", "myprotein", "decathlon", "holmes place", "fitness hut", "solinca", "go fit"]),
        (Categories.Subscriptions, EntryKind.Outflow, ["anthropic", "claude ai", "openai", "chatgpt", "apple com bill", "apple com", "icloud", "cloudflare", "gumroad", "shopify", "github", "google one", "google cloud", "microsoft", "notion", "adobe", "jetbrains", "obsidian"]),
        ("transport", EntryKind.Outflow, ["galp", "repsol", "cepsa", "prio ", "uber", "bolt ", "via verde", "brisa", "emel", "a s "]),
        ("leisure", EntryKind.Outflow, ["playstation", "steam", "nintendo", "xbox", "versus gamecenter", "galaxy games", "loaded com", "cinema", "karaoke", "ticketline", "bol pt", "fnac bilheteira"]),
        (Categories.OnlineShopping, EntryKind.Outflow, ["vinted", "ebay", "amazon", "amzn", "aliexpress", "shein", "temu", "vevor", "wish com", "etsy", "zalando"]),
        ("shopping", EntryKind.Outflow, ["fnac", "worten", "zara", "bershka", "pull bear", "primark", "el corte ingles", "pandora", "equivalenza", "cex ", "porto editora", "bertrand", "fixfone"]),
        ("home", EntryKind.Outflow, ["ikea", "leroy merlin", "aki ", "maxmat"]),
        ("loans", EntryKind.Outflow, ["cetelem", "bnp paribas", "cofidis", "unicre"]),
        (Categories.BankFees, EntryKind.Outflow, ["comissao", "transferencia com cartao debit", "transferencia imediata sepa", "imposto do selo", "manutencao de conta"]),
        ("refunds", EntryKind.Inflow, ["dev compra", "devolucao"]),
    ];

    private static readonly (string Key, EntryKind Kind, string[] Words)[] Rules =
    [
        (Categories.EatingOut, EntryKind.Outflow, ["lunch", "almoco", "dinner", "jantar", "breakfast", "pequeno almoco", "coffee", "cafe", "bakery", "restaurant", "restaurante", "takeaway", "snack", "lanche"]),
        ("groceries", EntryKind.Outflow, ["groceries", "supermarket", "supermercado", "mercado", "food", "comida"]),
        ("loans", EntryKind.Outflow, ["mortgage", "loan", "emprestimo", "prestacao"]),
        ("taxes", EntryKind.Outflow, ["tax", "taxes", "imposto", "impostos", "irs", "imi", "iuc", "financas", "seguranca social"]),
        ("insurance", EntryKind.Outflow, ["insurance", "seguro", "seguros", "service charge", "condominio"]),
        ("bills", EntryKind.Outflow, ["electricity", "eletricidade", "luz", "water", "agua", "gas", "internet", "fibre", "fibra", "phone", "telemovel", "mobile", "meo", "vodafone", "digi", "edp", "endesa", "goldenergy", "epal"]),
        ("transport", EntryKind.Outflow, ["fuel", "petrol", "diesel", "gasolina", "gasoleo", "combustivel", "bp", "bolt", "taxi", "metro", "carris", "cp", "comboio", "train", "bus", "autocarro", "navegante", "toll", "tolls", "portagem", "portagens", "parking", "estacionamento", "car wash", "lavagem", "inspecao", "oficina", "mechanic"]),
        ("vehicle", EntryKind.Outflow, ["car", "carro", "mota", "moto", "motorbike", "motorcycle", "scooter", "veiculo", "vehicle", "automovel"]),
        ("health", EntryKind.Outflow, ["pharmacy", "farmacia", "doctor", "medico", "consulta", "dentist", "dentista", "hospital", "clinic", "clinica", "physio", "fisioterapia", "optician", "oculista"]),
        ("gym", EntryKind.Outflow, ["gym", "ginasio", "fitness", "crossfit", "boxing", "boxe", "padel", "yoga", "pilates", "protein", "proteina", "supplements", "suplementos"]),
        ("leisure", EntryKind.Outflow, ["movies", "netflix", "spotify", "disney", "hbo", "concert", "concerto", "festival", "bar", "beer", "beers", "cerveja", "cervejas", "drinks", "copos", "game", "games", "jogo", "jogos", "holiday", "ferias", "trip", "viagem", "hotel", "flight", "voo", "museum", "museu"]),
        ("shopping", EntryKind.Outflow, ["shopping", "compras", "clothes", "roupa", "shoes", "sapatilhas", "sapatos", "gift", "prenda", "presente"]),
        ("home", EntryKind.Outflow, ["rent", "renda", "furniture", "moveis", "home", "casa"]),
        ("salary", EntryKind.Inflow, ["salary", "salario", "ordenado", "vencimento", "wage", "wages", "payroll"]),
        ("meal-allowance", EntryKind.Inflow, ["meal allowance", "subsidio de refeicao", "subsidio refeicao", "meal card", "cartao refeicao"]),
        ("freelance", EntryKind.Inflow, ["freelance", "invoice", "fatura", "recibo verde", "recibos verdes", "consulting", "consultoria", "side job", "biscate"]),
        ("dividends", EntryKind.Inflow, ["dividend", "dividends", "dividendo", "dividendos", "interest", "juros"]),
        ("vehicle-sale", EntryKind.Inflow, ["car", "carro", "mota", "moto", "motorbike", "motorcycle", "scooter", "veiculo", "vehicle", "automovel"]),
        ("refunds", EntryKind.Inflow, ["refund", "reembolso", "devolucao", "cashback"]),
    ];

    // Words that make a name a business rather than a person.
    private static readonly string[] Business = ["lda", "sa", "s a", "unip", "unipessoal", "limitada", "ltd", "inc", "gmbh", "bv", "com", "cia", "sociedade", "banco", "bank", "seguros", "estado"];

    // How a transfer to someone starts, before their name.
    private static readonly string[] TransferPrefixes = ["trf imediata ", "transferencia imediata ", "trf mb way ", "mb way ", "trf "];

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

        foreach (var (key, ruleKind, names) in Merchants)
        {
            if (ruleKind == kind && names.Any(name => words.Contains(" " + name.TrimEnd() + (name.EndsWith(' ') ? " " : string.Empty), StringComparison.Ordinal)))
            {
                return key;
            }
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

    /// <summary>
    /// Whether a bank name is a person ("Trf imediata MARIA SILVA", "Diogo Miguel Roseiro"): two to six words of
    /// letters, none of them a company word. Money between people is a split dinner or paying someone back.
    /// </summary>
    /// <param name="text">The name, as the bank wrote it.</param>
    /// <param name="sent">Money sent: then only a transfer ("Trf imediata …") counts, never a shop that reads like a name.</param>
    /// <returns>True for a person.</returns>
    public static bool IsPerson(string? text, bool sent = false)
    {
        var words = Words(text);
        var transfer = false;
        foreach (var prefix in TransferPrefixes)
        {
            if (words.StartsWith(prefix, StringComparison.Ordinal))
            {
                words = words[prefix.Length..];
                transfer = true;
                break;
            }
        }

        if (sent && !transfer)
        {
            return false;
        }

        var parts = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var padded = " " + words + " ";
        return parts.Length is >= 2 and <= 7
            && parts.All(static part => part.All(char.IsLetter))
            && !Business.Any(word => padded.Contains(" " + word + " ", StringComparison.Ordinal));
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
