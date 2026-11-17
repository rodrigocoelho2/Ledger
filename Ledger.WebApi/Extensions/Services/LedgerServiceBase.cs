using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Globalization;

namespace Ledger.WebApi.Extensions.Services;

/// <summary>
/// What every service of the ledger shares: reading the user's id and building the failure responses.
/// </summary>
/// <param name="globalizationHelper">Translations.</param>
internal abstract class LedgerServiceBase(IGlobalizationHelper globalizationHelper)
{
    /// <summary>The largest amount an entry or a balance can hold (DECIMAL (14, 2) with room to add up).</summary>
    public const decimal MaxAmount = 999_999_999.99m;

    /// <summary>Gets the translations.</summary>
    protected IGlobalizationHelper Globalization { get; } = globalizationHelper;

    /// <summary>Reads a user id as the Website sends it (32 hex characters).</summary>
    /// <param name="userId">The text.</param>
    /// <param name="id">The id.</param>
    /// <returns>True when it is an id.</returns>
    protected static bool TryUser(string? userId, out Guid id) => Guid.TryParse(userId, out id) && id != Guid.Empty;

    /// <summary>Formats an id the way the Website keeps them.</summary>
    /// <param name="id">The id.</param>
    /// <returns>32 hex characters.</returns>
    protected static string Id(Guid id) => id.ToString("N");

    /// <summary>Formats an optional id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>32 hex characters, or null.</returns>
    protected static string? Id(Guid? id) => id?.ToString("N");

    /// <summary>A "not found" response.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="what">What was looked for.</param>
    /// <returns>The response.</returns>
    protected async Task<ServiceApiResponse<T>> MissingAsync<T>(string? what) =>
        ServiceApiResponse<T>.NotFound(await Globalization.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), what ?? string.Empty));

    /// <summary>A "validation problems" response carrying the errors.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="errors">The errors.</param>
    /// <returns>The response.</returns>
    protected async Task<ServiceApiResponse<T>> InvalidAsync<T>(IEnumerable<ApiValidationError> errors) =>
        ServiceApiResponse<T>.ValidationProblems(await Globalization.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddErrorRange(errors);

    /// <summary>A "validation problems" response with one message that isn't about a field.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="field">The field it is closest to.</param>
    /// <param name="messageKey">The message.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The response.</returns>
    protected async Task<ServiceApiResponse<T>> ProblemAsync<T>(string field, string messageKey, params object[] arguments)
    {
        var message = await Globalization.TranslateAsync(messageKey, arguments);
        return ServiceApiResponse<T>.ValidationProblems(message).AddError(field, message);
    }

    /// <summary>One validation error "{field label} {message}".</summary>
    /// <param name="field">The field.</param>
    /// <param name="messageKey">The message.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The error.</returns>
    protected Task<ApiValidationError> ErrorAsync(string field, string messageKey, params object[] arguments) =>
        Globalization.ValidationErrorAsync(field, messageKey, arguments);
}
