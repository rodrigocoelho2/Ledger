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
    private const string FieldLanguage = "Language";

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

        await _settingRepository.SaveAsync(
            id,
            language,
            item.RentalEnabled ?? user.RentalEnabled ?? false,
            item.FlipsEnabled ?? user.FlipsEnabled ?? false);

        return ServiceApiResponse<UserDTO>.Success(UserService.ToDTO((await _userRepository.GetAsync(id)) ?? user));
    }
}
