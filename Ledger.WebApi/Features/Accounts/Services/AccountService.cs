using Ledger.Library.Enums;
using Ledger.Library.Services;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Globalization;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Accounts.Models.DTOs;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Accounts.Repositories;

namespace Ledger.WebApi.Features.Accounts.Services;

/// <summary>
/// The accounts.
/// </summary>
internal interface IAccountService
{
    /// <summary>Lists a user's accounts with their balances.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The accounts.</returns>
    Task<ServiceApiResponse<List<AccountDTO>>> ListAsync(string? userId);

    /// <summary>Adds or changes an account.</summary>
    /// <param name="item">The account.</param>
    /// <returns>The account, or validation problems.</returns>
    Task<ServiceApiResponse<AccountDTO>> SaveAsync(AccountSaveRequest item);

    /// <summary>Deletes an account (its entries stay).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The account.</param>
    /// <returns>The deleted id.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier);

    /// <summary>Keeps the order the accounts were dragged into.</summary>
    /// <param name="item">The order.</param>
    /// <returns>The accounts in their new order.</returns>
    Task<ServiceApiResponse<List<AccountDTO>>> ReorderAsync(AccountOrderRequest item);
}

/// <summary>
/// Account business logic.
/// </summary>
/// <param name="accountRepository">The accounts.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class AccountService(
    IAccountRepository accountRepository,
    IGlobalizationHelper globalizationHelper) : LedgerServiceBase(globalizationHelper), IAccountService
{
    /// <summary>The longest name of an account.</summary>
    public const int NameLength = 60;

    private const string FieldName = "Name";
    private const string FieldKind = "Kind";
    private const string FieldOpeningBalance = "OpeningBalance";

    private readonly IAccountRepository _accountRepository = accountRepository;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<AccountDTO>>> ListAsync(string? userId)
    {
        if (!TryUser(userId, out var id))
        {
            return await MissingAsync<List<AccountDTO>>(userId);
        }

        return ServiceApiResponse<List<AccountDTO>>.Success((await _accountRepository.ListAsync(id)).Select(ToDTO).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<AccountDTO>> SaveAsync(AccountSaveRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<AccountDTO>(item.UserId);
        }

        Guid? accountId = null;
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (!Guid.TryParse(item.Id, out var parsed))
            {
                return await MissingAsync<AccountDTO>(item.Id);
            }

            accountId = parsed;
        }

        var name = (item.Name ?? string.Empty).Trim();
        var openingBalance = decimal.Round(item.OpeningBalance ?? 0m, 2, MidpointRounding.AwayFromZero);

        var errors = new List<ApiValidationError>();
        if (name.Length == 0)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }
        else if (name.Length > NameLength)
        {
            errors.Add(await ErrorAsync(FieldName, nameof(TranslationKeys.TooLong), NameLength));
        }

        if (item.Kind is not { } kind || !Enum.IsDefined(kind))
        {
            errors.Add(await ErrorAsync(FieldKind, nameof(TranslationKeys.RequiredField)));
            kind = AccountKind.Current;
        }

        if (Math.Abs(openingBalance) > MaxAmount)
        {
            errors.Add(await ErrorAsync(FieldOpeningBalance, nameof(TranslationKeys.OutOfRange), -MaxAmount, MaxAmount));
        }

        if (errors.Count > 0)
        {
            return await InvalidAsync<AccountDTO>(errors);
        }

        var saved = await _accountRepository.SaveAsync(userId, accountId, name, kind, openingBalance);
        var row = saved is null ? null : (await _accountRepository.ListAsync(userId)).FirstOrDefault(account => account.AccountUniqueID == saved);
        return row is null ? await MissingAsync<AccountDTO>(item.Id ?? name) : ServiceApiResponse<AccountDTO>.Success(ToDTO(row));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string? userId, string? identifier)
    {
        if (!TryUser(userId, out var id) || !Guid.TryParse(identifier, out var accountId))
        {
            return await MissingAsync<string>(identifier);
        }

        if ((await _accountRepository.ListAsync(id)).All(account => account.AccountUniqueID != accountId))
        {
            return await MissingAsync<string>(identifier);
        }

        await _accountRepository.DeleteAsync(id, accountId);
        return ServiceApiResponse<string>.Success(Id(accountId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<AccountDTO>>> ReorderAsync(AccountOrderRequest item)
    {
        if (!TryUser(item.UserId, out var userId))
        {
            return await MissingAsync<List<AccountDTO>>(item.UserId);
        }

        var order = (item.Order ?? [])
            .Select(static text => Guid.TryParse(text, out var id) ? id : Guid.Empty)
            .Where(static id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (order.Count > 0)
        {
            await _accountRepository.ReorderAsync(userId, order);
        }

        return ServiceApiResponse<List<AccountDTO>>.Success((await _accountRepository.ListAsync(userId)).Select(ToDTO).ToList());
    }

    /// <summary>Maps a row to what the Website sees.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The account.</returns>
    internal static AccountDTO ToDTO(AccountRow row) => new()
    {
        Id = Id(row.AccountUniqueID),
        Name = row.Name,
        Kind = Enum.IsDefined((AccountKind)row.Kind) ? (AccountKind)row.Kind : AccountKind.Current,
        OpeningBalance = row.OpeningBalance,
        Balance = row.Balance,
    };
}
