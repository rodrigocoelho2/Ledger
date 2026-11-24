using System.Globalization;
using System.Text;
using System.Text.Json;
using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.WebApi.Features.Entries.Services;

namespace Ledger.WebApi.Features.Backup.Services;

/// <summary>An account as a file names it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Kind">Its kind (null: guessed from the name).</param>
/// <param name="OpeningBalance">What it starts with.</param>
internal sealed record FileAccount(string Name, AccountKind? Kind, decimal OpeningBalance);

/// <summary>A project as a file names it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="TargetSale">The price hoped for.</param>
/// <param name="WorksBudget">What the works should cost.</param>
/// <param name="AgreedPrice">The price agreed with the seller.</param>
internal sealed record FileProject(string Name, decimal? TargetSale, decimal? WorksBudget, decimal? AgreedPrice = null);

/// <summary>An entry as a file writes it: the account and the project by name.</summary>
/// <param name="Day">The day.</param>
/// <param name="Kind">Which way the money moved.</param>
/// <param name="Scope">The part of life.</param>
/// <param name="Name">What it was.</param>
/// <param name="Category">The category key (null on transfers).</param>
/// <param name="Amount">The amount.</param>
/// <param name="Account">The account's name (empty: the person's first account).</param>
/// <param name="ToAccount">The account a transfer goes to.</param>
/// <param name="Project">The project's name.</param>
/// <param name="Nights">The nights of a booking.</param>
/// <param name="Channel">Where a booking came from.</param>
/// <param name="Plan">The instalment plan it belongs to (null: none).</param>
/// <param name="PlanPart">Which instalment it is.</param>
/// <param name="PlanParts">How many instalments the plan has.</param>
/// <param name="External">The bank's id for it, when it came from the bank (JSON only).</param>
/// <param name="ExternalTo">The bank's id of the arriving side of a transfer between connected accounts (JSON only).</param>
internal sealed record FileEntry(
    DateOnly Day,
    EntryKind Kind,
    EntryScope Scope,
    string Name,
    string? Category,
    decimal Amount,
    string Account,
    string? ToAccount,
    string? Project,
    int? Nights,
    string? Channel,
    Guid? Plan = null,
    int? PlanPart = null,
    int? PlanParts = null,
    string? External = null,
    string? ExternalTo = null);

/// <summary>Everything read from a file.</summary>
internal sealed class FileLedger
{
    /// <summary>Gets the accounts the file describes (a CSV describes none: they are guessed from the entries).</summary>
    public List<FileAccount> Accounts { get; } = [];

    /// <summary>Gets the projects the file describes.</summary>
    public List<FileProject> Projects { get; } = [];

    /// <summary>Gets the spending caps, by the first day of the month.</summary>
    public Dictionary<DateOnly, decimal> Caps { get; } = [];

    /// <summary>Gets the entries.</summary>
    public List<FileEntry> Entries { get; } = [];

    /// <summary>Gets or sets how many rows could not be read.</summary>
    public int Skipped { get; set; }
}

/// <summary>
/// Reads and writes the files of a ledger: the CSV (one entry per line, for a spreadsheet or an accountant) and the
/// JSON backup (everything, to bring back later). Reading also takes the CSV and the JSON the single-file version of
/// Ledger exported, with its Portuguese headers and category names. No database, no clock.
/// </summary>
internal static class LedgerFile
{
    /// <summary>The value of "app" in a JSON backup.</summary>
    public const string AppName = "ledger";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly string[] Header = ["date", "kind", "description", "category", "scope", "account", "to_account", "project", "nights", "channel", "plan", "instalment", "amount"];

    // Simplified column titles (lower case, no accents or punctuation) → the column.
    private static readonly Dictionary<string, string> Columns = new(StringComparer.Ordinal)
    {
        ["date"] = "date", ["data"] = "date", ["day"] = "date", ["dia"] = "date",
        ["kind"] = "kind", ["tipo"] = "kind", ["type"] = "kind",
        ["description"] = "name", ["descricao"] = "name", ["name"] = "name", ["nome"] = "name",
        ["category"] = "category", ["categoria"] = "category",
        ["scope"] = "scope", ["ambito"] = "scope",
        ["account"] = "account", ["conta"] = "account",
        ["toaccount"] = "to", ["contadestino"] = "to", ["to"] = "to", ["para"] = "to",
        ["project"] = "project", ["projeto"] = "project",
        ["nights"] = "nights", ["noites"] = "nights",
        ["channel"] = "channel", ["canal"] = "channel",
        ["plan"] = "plan", ["plano"] = "plan",
        ["instalment"] = "instalment", ["installment"] = "instalment", ["prestacao"] = "instalment",
        ["amount"] = "amount", ["valor"] = "amount", ["value"] = "amount",
    };

    // Names and ids the single-file version used that today's list no longer has.
    private static readonly Dictionary<string, string> OldCategories = new(StringComparer.Ordinal)
    {
        ["casa"] = "home", ["contas"] = "bills", ["contasutilities"] = "bills", ["credito"] = "loans", ["impostos"] = "taxes",
        ["ginasio"] = "gym", ["compras"] = "shopping", ["transportes"] = "transport", ["lazer"] = "leisure", ["saude"] = "health",
        ["limpezaal"] = "cleaning", ["rentalcleaning"] = "cleaning", ["comissoes"] = "platform-fees", ["comissoesplataforma"] = "platform-fees",
        ["manutencaoobras"] = "maintenance", ["condominio"] = "insurance", ["consumiveis"] = "guest-supplies",
        ["consumiveishospedes"] = "guest-supplies", ["aquisicaodoimovel"] = "purchase", ["propertypurchase"] = "purchase",
        ["escritura"] = "deed", ["obras"] = "works", ["maoobra"] = "labour", ["licencas"] = "permits", ["mediacao"] = "agency",
        ["outros"] = "other", ["outrosin"] = "other-income", ["salario"] = "salary", ["dividendos"] = "dividends",
        ["reservasal"] = "bookings", ["rentalbookings"] = "bookings", ["venda"] = "sale", ["vendadeimovel"] = "sale",
        ["reembolso"] = "refunds", ["groceriesfood"] = "groceries",
    };

    /// <summary>Whether a file's text looks like JSON rather than CSV.</summary>
    /// <param name="content">The text.</param>
    /// <returns>True for JSON.</returns>
    public static bool LooksLikeJson(string content) => content.AsSpan().TrimStart('﻿').TrimStart().StartsWith("{", StringComparison.Ordinal);

    // ------------------------------------------------------------------ writing

    /// <summary>
    /// Writes the entries as CSV: semicolons, a header line, UTF-8 with a byte order mark so a spreadsheet opens it
    /// with the accents right. In Portuguese the category and scope names are Portuguese and the cents come after a comma.
    /// </summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <param name="language">The language of the names ("en" or "pt").</param>
    /// <returns>The text of the file.</returns>
    public static string WriteCsv(IEnumerable<FileEntry> entries, string? language)
    {
        var portuguese = Languages.IsPortuguese(language);
        var culture = portuguese ? CultureInfo.GetCultureInfo("pt-PT") : CultureInfo.InvariantCulture;
        var builder = new StringBuilder("﻿");
        builder.AppendJoin(';', Header).Append('\n');
        foreach (var entry in entries)
        {
            string[] cells =
            [
                entry.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entry.Kind switch { EntryKind.Inflow => "in", EntryKind.Transfer => "transfer", _ => "out" },
                entry.Name,
                entry.Category is null ? string.Empty : Categories.Name(entry.Category, language),
                ScopeName(entry.Scope, portuguese),
                entry.Account,
                entry.ToAccount ?? string.Empty,
                entry.Project ?? string.Empty,
                entry.Nights?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                entry.Channel ?? string.Empty,
                entry.Plan?.ToString("N") ?? string.Empty,
                entry.Plan is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{entry.PlanPart}/{entry.PlanParts}"),
                entry.Amount.ToString("0.00", culture),
            ];
            builder.AppendJoin(';', cells.Select(Quote)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Writes a whole ledger as the JSON backup.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="exportedAt">When it was exported.</param>
    /// <returns>The text of the file.</returns>
    public static string WriteJson(FileLedger ledger, DateTimeOffset exportedAt) => JsonSerializer.Serialize(
        new BackupJson
        {
            App = AppName,
            Version = 1,
            ExportedAt = exportedAt,
            Accounts = [.. ledger.Accounts.Select(static account => new BackupAccount { Name = account.Name, Kind = (account.Kind ?? AccountKind.Current).ToString(), OpeningBalance = account.OpeningBalance })],
            Projects = [.. ledger.Projects.Select(static project => new BackupProject { Name = project.Name, TargetSale = project.TargetSale, WorksBudget = project.WorksBudget, AgreedPrice = project.AgreedPrice })],
            Caps = [.. ledger.Caps.OrderBy(static cap => cap.Key).Select(static cap => new BackupCap { Month = Months.Text(cap.Key), Amount = cap.Value })],
            Entries = [.. ledger.Entries.Select(static entry => new BackupEntry
            {
                Date = entry.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Kind = entry.Kind.ToString(),
                Scope = entry.Scope.ToString(),
                Name = entry.Name,
                Category = entry.Category,
                Account = entry.Account,
                ToAccount = entry.ToAccount,
                Project = entry.Project,
                Nights = entry.Nights,
                Channel = entry.Channel,
                Plan = entry.Plan?.ToString("N"),
                PlanPart = entry.PlanPart,
                PlanParts = entry.PlanParts,
                External = entry.External,
                ExternalTo = entry.ExternalTo,
                Amount = entry.Amount,
            })],
        },
        JsonOptions);

    // ------------------------------------------------------------------ reading

    /// <summary>
    /// Reads a file: a JSON backup, the JSON of the single-file version, or a CSV.
    /// </summary>
    /// <param name="content">The text of the file.</param>
    /// <returns>What was read, or null when the file isn't one Ledger understands.</returns>
    public static FileLedger? Read(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return LooksLikeJson(content) ? ReadJson(content) : ReadCsv(content);
    }

    /// <summary>Reads a CSV: the first line names the columns (English or Portuguese), in any order.</summary>
    /// <param name="content">The text of the file.</param>
    /// <returns>What was read, or null when the date, description and amount columns aren't all there.</returns>
    public static FileLedger? ReadCsv(string content)
    {
        var rows = SplitCsv(content);
        if (rows.Count == 0)
        {
            return null;
        }

        var at = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < rows[0].Count; index++)
        {
            if (Columns.TryGetValue(Categories.Simplify(rows[0][index]), out var column))
            {
                at.TryAdd(column, index);
            }
        }

        if (!at.ContainsKey("date") || !at.ContainsKey("name") || !at.ContainsKey("amount"))
        {
            return null;
        }

        var ledger = new FileLedger();
        foreach (var row in rows.Skip(1))
        {
            string Cell(string column) => at.TryGetValue(column, out var index) && index < row.Count ? row[index].Trim() : string.Empty;

            var amountText = Cell("amount");
            var name = Cell("name");
            if (!TryDay(Cell("date"), out var day) || name.Length == 0 || !QuickAdd.TryAmount(amountText, out var amount) || amount <= 0)
            {
                ledger.Skipped++;
                continue;
            }

            var kind = Kind(Cell("kind"));
            var nights = int.TryParse(Cell("nights"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count is > 0 and <= 365 ? count : (int?)null;
            var (plan, part, parts) = Plan(Cell("plan"), Cell("instalment"));
            ledger.Entries.Add(new FileEntry(
                day,
                kind,
                Scope(Cell("scope")),
                Short(name, EntryService.NameLength),
                kind == EntryKind.Transfer ? null : Category(Cell("category"), kind),
                amount,
                Cell("account"),
                kind == EntryKind.Transfer ? Empty(Cell("to")) : null,
                Empty(Cell("project")),
                nights,
                Empty(Short(Cell("channel"), EntryService.ChannelLength)),
                plan,
                part,
                parts));
        }

        return ledger;
    }

    /// <summary>The instalment plan of a line: its id and "2/6".</summary>
    /// <param name="id">The plan's id.</param>
    /// <param name="instalment">Which instalment of how many ("2/6").</param>
    /// <returns>The plan, or nothing when either is missing or makes no sense.</returns>
    internal static (Guid? Plan, int? Part, int? Parts) Plan(string? id, string? instalment)
    {
        if (!Guid.TryParse(id, out var plan) || string.IsNullOrWhiteSpace(instalment))
        {
            return (null, null, null);
        }

        var pieces = instalment.Split('/');
        return pieces.Length == 2
            && int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var part)
            && int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parts)
            && parts is >= 2 and <= 60 && part >= 1 && part <= parts
                ? (plan, part, parts)
                : (null, null, null);
    }

    /// <summary>Reads a JSON file: Ledger's own backup, or what the single-file version exported.</summary>
    /// <param name="content">The text of the file.</param>
    /// <returns>What was read, or null when it is neither.</returns>
    public static FileLedger? ReadJson(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content.TrimStart('﻿'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("tx", out var old) && old.ValueKind == JsonValueKind.Array)
            {
                return ReadSingleFile(root, old);
            }

            var backup = root.Deserialize<BackupJson>(JsonOptions);
            if (backup is null || !string.Equals(backup.App, AppName, StringComparison.OrdinalIgnoreCase) || backup.Entries is null)
            {
                return null;
            }

            var ledger = new FileLedger();
            foreach (var account in backup.Accounts ?? [])
            {
                if (!string.IsNullOrWhiteSpace(account.Name))
                {
                    ledger.Accounts.Add(new FileAccount(account.Name.Trim(), Enum.TryParse<AccountKind>(account.Kind, true, out var kind) && Enum.IsDefined(kind) ? kind : null, account.OpeningBalance));
                }
            }

            foreach (var project in backup.Projects ?? [])
            {
                if (!string.IsNullOrWhiteSpace(project.Name))
                {
                    ledger.Projects.Add(new FileProject(project.Name.Trim(), project.TargetSale, project.WorksBudget, project.AgreedPrice));
                }
            }

            foreach (var cap in backup.Caps ?? [])
            {
                if (cap.Month is { Length: 7 } && cap.Amount > 0)
                {
                    ledger.Caps[Months.Parse(cap.Month, default)] = cap.Amount;
                }
            }

            foreach (var entry in backup.Entries)
            {
                if (!TryDay(entry.Date, out var day) || string.IsNullOrWhiteSpace(entry.Name) || entry.Amount <= 0
                    || !Enum.TryParse<EntryKind>(entry.Kind, true, out var kind) || !Enum.IsDefined(kind))
                {
                    ledger.Skipped++;
                    continue;
                }

                var scope = Enum.TryParse<EntryScope>(entry.Scope, true, out var part) && Enum.IsDefined(part) ? part : EntryScope.Personal;
                var (plan, planPart, planParts) = Plan(entry.Plan, entry.PlanPart is { } p && entry.PlanParts is { } n ? string.Create(CultureInfo.InvariantCulture, $"{p}/{n}") : null);
                ledger.Entries.Add(new FileEntry(
                    day,
                    kind,
                    scope,
                    Short(entry.Name.Trim(), EntryService.NameLength),
                    kind == EntryKind.Transfer ? null : Category(entry.Category, kind),
                    decimal.Round(entry.Amount, 2, MidpointRounding.AwayFromZero),
                    entry.Account?.Trim() ?? string.Empty,
                    kind == EntryKind.Transfer ? Empty(entry.ToAccount) : null,
                    Empty(entry.Project),
                    entry.Nights is > 0 and <= 365 ? entry.Nights : null,
                    Empty(Short(entry.Channel ?? string.Empty, EntryService.ChannelLength)),
                    plan,
                    planPart,
                    planParts,
                    Empty(Short(entry.External ?? string.Empty, 100)),
                    kind == EntryKind.Transfer ? Empty(Short(entry.ExternalTo ?? string.Empty, 100)) : null));
            }

            return ledger;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads a kind by any of its words.</summary>
    /// <param name="text">"out", "in", "transfer" and their Portuguese and long forms.</param>
    /// <returns>The kind; an outflow when nothing says otherwise.</returns>
    public static EntryKind Kind(string? text) => Categories.Simplify(text) switch
    {
        "in" or "inflow" or "entrada" or "receita" or "income" => EntryKind.Inflow,
        "move" or "transfer" or "transferencia" => EntryKind.Transfer,
        _ => EntryKind.Outflow,
    };

    /// <summary>Reads a scope by any of its names.</summary>
    /// <param name="text">The name.</param>
    /// <returns>The scope; personal when nothing says otherwise.</returns>
    public static EntryScope Scope(string? text) => Categories.Simplify(text) switch
    {
        "rental" or "shorttermrental" or "alojamentolocal" or "al" => EntryScope.Rental,
        "flips" or "flip" or "buysell" or "compravenda" => EntryScope.Flips,
        _ => EntryScope.Personal,
    };

    /// <summary>Finds a category by key or by name in either language, old names included.</summary>
    /// <param name="text">The key or the name.</param>
    /// <param name="kind">Inflow or outflow.</param>
    /// <returns>The key; "other" or "other-income" when nothing matches.</returns>
    public static string Category(string? text, EntryKind kind)
    {
        if (Categories.Match(text, kind) is { } match)
        {
            return match.Key;
        }

        return OldCategories.TryGetValue(Categories.Simplify(text), out var key) && Categories.Find(key)?.Kind == kind
            ? key
            : Categories.Fallback(kind);
    }

    /// <summary>Guesses an account's kind from its name, for files that don't say it.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The kind.</returns>
    public static AccountKind GuessKind(string name)
    {
        var simple = Categories.Simplify(name);
        return simple.Contains("poupan", StringComparison.Ordinal) || simple.Contains("saving", StringComparison.Ordinal) ? AccountKind.Savings
            : simple.Contains("invest", StringComparison.Ordinal) ? AccountKind.Investments
            : simple.Contains("refei", StringComparison.Ordinal) || simple.Contains("meal", StringComparison.Ordinal) ? AccountKind.MealCard
            : AccountKind.Current;
    }

    /// <summary>Reads a day written the ISO way or the Portuguese way.</summary>
    /// <param name="text">"2026-10-30", "30/10/2026" or "30-10-2026".</param>
    /// <param name="day">The day.</param>
    /// <returns>True when it is a day.</returns>
    public static bool TryDay(string? text, out DateOnly day) =>
        DateOnly.TryParseExact((text ?? string.Empty).Trim(), ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out day)
        && day.Year is >= 1990 and <= 2200;

    /// <summary>
    /// Splits CSV text into rows of cells: semicolons or commas (whichever the header uses more), quotes around a cell
    /// with the separator or a line break in it, doubled quotes inside.
    /// </summary>
    /// <param name="content">The text.</param>
    /// <returns>The rows that have anything in them.</returns>
    public static List<List<string>> SplitCsv(string content)
    {
        var text = content.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var firstLine = text.Split('\n', 2)[0];
        var separator = firstLine.Count(static character => character == ';') >= firstLine.Count(static character => character == ',') ? ';' : ',';

        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == separator)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (character == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                cell.Append(character);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return [.. rows.Where(static cells => cells.Any(static value => !string.IsNullOrWhiteSpace(value)))];
    }

    private static FileLedger ReadSingleFile(JsonElement root, JsonElement entries)
    {
        var ledger = new FileLedger();
        var accountNames = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array)
        {
            foreach (var account in accounts.EnumerateArray())
            {
                var name = Text(account, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                accountNames[Text(account, "id")] = name;
                AccountKind? kind = Text(account, "kind") switch
                {
                    "poupanca" => AccountKind.Savings,
                    "investimento" => AccountKind.Investments,
                    "refeicao" => AccountKind.MealCard,
                    "corrente" => AccountKind.Current,
                    _ => null,
                };
                ledger.Accounts.Add(new FileAccount(name, kind, Number(account, "start")));
            }
        }

        if (root.TryGetProperty("caps", out var caps) && caps.ValueKind == JsonValueKind.Object)
        {
            foreach (var cap in caps.EnumerateObject())
            {
                if (cap.Name.Length == 7 && cap.Value.ValueKind == JsonValueKind.Number && cap.Value.TryGetDecimal(out var amount) && amount > 0)
                {
                    ledger.Caps[Months.Parse(cap.Name, default)] = amount;
                }
            }
        }

        foreach (var entry in entries.EnumerateArray())
        {
            var name = Text(entry, "name");
            var amount = decimal.Round(Number(entry, "amount"), 2, MidpointRounding.AwayFromZero);
            if (!TryDay(Text(entry, "date"), out var day) || name.Length == 0 || amount <= 0)
            {
                ledger.Skipped++;
                continue;
            }

            var kind = Kind(Text(entry, "k"));
            ledger.Entries.Add(new FileEntry(
                day,
                kind,
                Scope(Text(entry, "scope")),
                Short(name, EntryService.NameLength),
                kind == EntryKind.Transfer ? null : Category(Text(entry, "catId"), kind),
                amount,
                accountNames.GetValueOrDefault(Text(entry, "accId"), string.Empty),
                kind == EntryKind.Transfer ? Empty(accountNames.GetValueOrDefault(Text(entry, "toId"), string.Empty)) : null,
                Empty(Text(entry, "project")),
                null,
                null));
        }

        return ledger;
    }

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static decimal Number(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number
            : 0m;

    private static string ScopeName(EntryScope scope, bool portuguese) => scope switch
    {
        EntryScope.Rental => portuguese ? "Alojamento local" : "Short-term rental",
        EntryScope.Flips => portuguese ? "Compra & Venda" : "Buy & Sell",
        _ => portuguese ? "Pessoal" : "Personal",
    };

    private static string Quote(string cell) =>
        cell.Contains(';', StringComparison.Ordinal) || cell.Contains('"', StringComparison.Ordinal) || cell.Contains('\n', StringComparison.Ordinal)
            ? "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : cell;

    private static string Short(string text, int length) => text.Length <= length ? text : text[..length];

    private static string? Empty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private sealed class BackupJson
    {
        public string? App { get; set; }

        public int Version { get; set; }

        public DateTimeOffset ExportedAt { get; set; }

        public List<BackupAccount>? Accounts { get; set; }

        public List<BackupProject>? Projects { get; set; }

        public List<BackupCap>? Caps { get; set; }

        public List<BackupEntry>? Entries { get; set; }
    }

    private sealed class BackupAccount
    {
        public string? Name { get; set; }

        public string? Kind { get; set; }

        public decimal OpeningBalance { get; set; }
    }

    private sealed class BackupProject
    {
        public string? Name { get; set; }

        public decimal? TargetSale { get; set; }

        public decimal? WorksBudget { get; set; }

        public decimal? AgreedPrice { get; set; }
    }

    private sealed class BackupCap
    {
        public string? Month { get; set; }

        public decimal Amount { get; set; }
    }

    private sealed class BackupEntry
    {
        public string? Date { get; set; }

        public string? Kind { get; set; }

        public string? Scope { get; set; }

        public string? Name { get; set; }

        public string? Category { get; set; }

        public string? Account { get; set; }

        public string? ToAccount { get; set; }

        public string? Project { get; set; }

        public int? Nights { get; set; }

        public string? Channel { get; set; }

        public string? Plan { get; set; }

        public int? PlanPart { get; set; }

        public int? PlanParts { get; set; }

        public string? External { get; set; }

        public string? ExternalTo { get; set; }

        public decimal Amount { get; set; }
    }
}
