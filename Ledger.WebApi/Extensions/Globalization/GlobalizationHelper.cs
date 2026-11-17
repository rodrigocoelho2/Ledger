using System.Globalization;
using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Validation;

namespace Ledger.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys and field identifiers into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>Gets the language of the request ("en" or "pt"), from its Accept-Language header.</summary>
    string Language { get; }

    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name or a field identifier.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error "{field label} {message}".
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// The user-facing messages, in English and Portuguese. The app has two languages and a few dozen messages, so the
/// texts live in code rather than in a translation table. The Website sends the person's language with every call.
/// </summary>
/// <param name="httpContextAccessor">The request (for its language).</param>
internal sealed class GlobalizationHelper(IHttpContextAccessor httpContextAccessor) : IGlobalizationHelper
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    private static readonly Dictionary<string, (string English, string Portuguese)> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = ("Some of the data sent is not valid.", "Alguns dos dados enviados não são válidos."),
        [nameof(TranslationKeys.RequiredField)] = ("is required.", "é obrigatório."),
        [nameof(TranslationKeys.AlreadyExists)] = ("'{0}' is already taken.", "'{0}' já está em uso."),
        [nameof(TranslationKeys.NoRecordsFound)] = ("'{0}' does not exist.", "'{0}' não existe."),
        [nameof(TranslationKeys.UserNameInvalid)] = ("must be 3–32 letters, numbers, dots, dashes or underscores.", "tem de ter 3 a 32 letras, números, pontos, hífenes ou underscores."),
        [nameof(TranslationKeys.PasswordTooShort)] = ("must have at least {0} characters.", "tem de ter pelo menos {0} caracteres."),
        [nameof(TranslationKeys.PasswordTooLong)] = ("is too long.", "é demasiado longa."),
        [nameof(TranslationKeys.WrongCredentials)] = ("Wrong user name or password.", "Nome de utilizador ou palavra-passe errados."),
        [nameof(TranslationKeys.WrongCurrentPassword)] = ("is wrong.", "está errada."),
        [nameof(TranslationKeys.LastAdmin)] = ("The last admin can't be deleted.", "O último administrador não pode ser apagado."),
        [nameof(TranslationKeys.OutOfRange)] = ("must be between {0} and {1}.", "tem de estar entre {0} e {1}."),
        [nameof(TranslationKeys.TooLong)] = ("can have at most {0} characters.", "pode ter no máximo {0} caracteres."),
        [nameof(TranslationKeys.AmountInvalid)] = ("must be above zero.", "tem de ser acima de zero."),
        [nameof(TranslationKeys.DateInvalid)] = ("isn't a day Ledger can keep.", "não é um dia que o Ledger consiga guardar."),
        [nameof(TranslationKeys.CategoryUnknown)] = ("isn't one of the categories for this kind of entry.", "não é uma das categorias deste tipo de entrada."),
        [nameof(TranslationKeys.AccountUnknown)] = ("isn't one of your accounts.", "não é uma das tuas contas."),
        [nameof(TranslationKeys.SameAccount)] = ("A transfer needs two different accounts.", "Uma transferência precisa de duas contas diferentes."),
        [nameof(TranslationKeys.ProjectUnknown)] = ("isn't one of your projects.", "não é um dos teus projetos."),
        [nameof(TranslationKeys.LanguageUnknown)] = ("must be English or Portuguese.", "tem de ser inglês ou português."),
        [nameof(TranslationKeys.FileInvalid)] = ("This isn't a Ledger export: a CSV needs at least the date, description and amount columns.", "Isto não é uma exportação do Ledger: um CSV precisa pelo menos das colunas data, descrição e valor."),
        [nameof(TranslationKeys.FileEmpty)] = ("The file had no entries Ledger could read.", "O ficheiro não tinha entradas que o Ledger conseguisse ler."),
        [nameof(TranslationKeys.QuickAddNoAmount)] = ("Start with an amount, then a few words: 9.80 lunch.", "Começa pelo valor e depois umas palavras: 9,80 almoço."),
        [nameof(TranslationKeys.DiscordNotLinked)] = ("No user here is linked to that Discord account. Sign in with your password and link it in Settings.", "Nenhum utilizador aqui está ligado a essa conta Discord. Entra com a palavra-passe e liga-a nas Definições."),
        [nameof(TranslationKeys.DiscordTaken)] = ("That Discord account is already linked to another user.", "Essa conta Discord já está ligada a outro utilizador."),
        [nameof(TranslationKeys.DiscordInvalid)] = ("isn't a Discord account.", "não é uma conta Discord."),
        [nameof(TranslationKeys.DiscordNeedsPassword)] = ("Discord is the only way into this user. Set a password before unlinking it.", "O Discord é a única forma de entrar neste utilizador. Define uma palavra-passe antes de o desligar."),

        ["UserName"] = ("User name", "O nome de utilizador"),
        ["Password"] = ("Password", "A palavra-passe"),
        ["CurrentPassword"] = ("Current password", "A palavra-passe atual"),
        ["NewPassword"] = ("New password", "A nova palavra-passe"),
        ["Identifier"] = ("User", "O utilizador"),
        ["DiscordId"] = ("Discord account", "A conta Discord"),
        ["Name"] = ("Name", "O nome"),
        ["Kind"] = ("Kind", "O tipo"),
        ["Scope"] = ("Scope", "O âmbito"),
        ["Amount"] = ("Amount", "O valor"),
        ["OpeningBalance"] = ("Opening balance", "O saldo inicial"),
        ["Date"] = ("Date", "A data"),
        ["Category"] = ("Category", "A categoria"),
        ["AccountId"] = ("Account", "A conta"),
        ["ToAccountId"] = ("Destination account", "A conta de destino"),
        ["ProjectId"] = ("Project", "O projeto"),
        ["Nights"] = ("Nights", "As noites"),
        ["Channel"] = ("Channel", "O canal"),
        ["Language"] = ("Language", "O idioma"),
        ["Month"] = ("Month", "O mês"),
        ["TargetSale"] = ("Target sale price", "O preço de venda alvo"),
        ["WorksBudget"] = ("Works budget", "O orçamento das obras"),
        ["AgreedPrice"] = ("Agreed price", "O preço acordado"),
        ["File"] = ("File", "O ficheiro"),
        ["Text"] = ("Quick add", "A entrada rápida"),
    };

    /// <inheritdoc />
    public string Language => Languages.Normalize(httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString());

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var portuguese = Language == Languages.Portuguese;
        var text = Translations.TryGetValue(key, out var value) ? (portuguese ? value.Portuguese : value.English) : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(portuguese ? Portuguese : English, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments)
    {
        var label = await TranslateAsync(field);
        var message = await TranslateAsync(messageKey, arguments);
        return new ApiValidationError(field, $"{label} {message}");
    }
}
