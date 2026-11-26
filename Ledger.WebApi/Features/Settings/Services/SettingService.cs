using Ledger.Library.Categories;
using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Settings.Models.DTOs;
using Ledger.WebApi.Features.Settings.Repositories;
using Ledger.WebApi.Features.Users.Models.DTOs;
using Ledger.WebApi.Features.Users.Repositories;
using Ledger.WebApi.Features.Users.Services;

namespace Ledger.WebApi.Features.Settings.Services;

/// <summary>
/// What a person chose in Settings: the language and the optional modules.
/// </summary>
internal interface ISettingService
{
    /// <summary>Keeps the language and which modules are on. What the request leaves empty stays as it is.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The user, with the choices as they are now.</returns>
    Task<ServiceApiResponse<UserDTO>> SaveAsync(SettingSaveRequest item);
}

/// <summary>
/// Settings business logic.
/// </summary>
/// <param name="settingRepository">The settings.</param>
/// <param name="userRepository">The users.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class SettingService(
    ISettingRepository settingRepository,
    IUserRepository userRepository,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), ISettingService
{
    /// <summary>The longest word kept to recognise the salary.</summary>
    public const int PayerLength = 64;

    private const string FieldLanguage = "Language";
    private const string FieldPayDay = "PayDay";
    private const string FieldPayer = "Payer";

    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IUserRepository _userRepository = userRepository;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> SaveAsync(SettingSaveRequest item)
    {
        var user = TryUser(item.UserId, out var id) ? await _userRepository.GetAsync(id) : null;
        if (user is null)
        {
            return await MissingAsync<UserDTO>(item.UserId);
        }

        var language = string.IsNullOrWhiteSpace(item.Language) ? Languages.Normalize(user.Language) : item.Language.Trim().ToLowerInvariant();
        if (language is not (Languages.English or Languages.Portuguese))
        {
            return await InvalidAsync<UserDTO>([await ErrorAsync(FieldLanguage, nameof(TranslationKeys.LanguageUnknown))]);
        }

        if (item.PayDay is > 31 or < 0)
        {
            return await InvalidAsync<UserDTO>([await ErrorAsync(FieldPayDay, nameof(TranslationKeys.OutOfRange), 1, 31)]);
        }

        // A payday of 0 or 1 goes back to calendar months; empty leaves the payday as it is.
        var payDay = item.PayDay is { } day ? (day <= 1 ? null : (byte?)day) : user.PayDay;

        var payer = item.Payer is null ? user.Payer : string.IsNullOrWhiteSpace(item.Payer) ? null : item.Payer.Trim();
        if (payer is { Length: > PayerLength })
        {
            return await InvalidAsync<UserDTO>([await ErrorAsync(FieldPayer, nameof(TranslationKeys.OutOfRange), 1, PayerLength)]);
        }

        await _settingRepository.SaveAsync(
            id,
            language,
            item.RentalEnabled ?? user.RentalEnabled ?? false,
            item.FlipsEnabled ?? user.FlipsEnabled ?? false,
            payDay,
            payDay is null ? null : payer);

        return ServiceApiResponse<UserDTO>.Success(UserService.ToDTO((await _userRepository.GetAsync(id)) ?? user));
    }
}
