using System.Collections.Concurrent;
using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Http;
using Ledger.WebApi.Features.Accounts.Repositories;
using Ledger.WebApi.Features.Users.Repositories;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Accounts.Services;

/// <summary>
/// The accounts valued elsewhere: their value is asked for and kept, a value a day.
/// </summary>
internal interface IValuationService
{
    /// <summary>Asks for one account's value now and keeps it.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Null when it worked, else what went wrong.</returns>
    Task<string?> RefreshAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Asks for every valued account's value (the background worker).</summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>How many values were kept.</returns>
    Task<int> RefreshAllAsync(CancellationToken cancellationToken);

    /// <summary>What went wrong the last time an account was asked for, if it did.</summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The error, or null.</returns>
    string? ErrorOf(Guid accountId);
}

/// <summary>
/// Values from Obol: the account's user name there (or the same as in Ledger), its portfolio's value, kept for today.
/// </summary>
/// <param name="accountRepository">The accounts.</param>
/// <param name="userRepository">The users (for the Ledger user name).</param>
/// <param name="obol">Obol.</param>
/// <param name="options">The app's options.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed class ValuationService(
    IAccountRepository accountRepository,
    IUserRepository userRepository,
    IObolClient obol,
    IOptions<LedgerOptions> options,
    IAppClock clock,
    ILogger<ValuationService> logger) : IValuationService
{
    private readonly ConcurrentDictionary<Guid, string> _errors = new();

    /// <inheritdoc />
    public async Task<string?> RefreshAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = (await accountRepository.ListAsync(userId)).FirstOrDefault(row => row.AccountUniqueID == accountId);
        if (account?.ValueSource is null)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(account.ValueRef) ? (await userRepository.GetAsync(userId))?.Username : account.ValueRef;
        return await ValueAsync(userId, accountId, (ValueSource)account.ValueSource.Value, name ?? string.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RefreshAllAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ObolEnabled)
        {
            return 0;
        }

        var kept = 0;
        foreach (var account in await accountRepository.ValuedAllAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = string.IsNullOrWhiteSpace(account.ValueRef) ? account.Username : account.ValueRef;
            if (await ValueAsync(account.UserUniqueID, account.AccountUniqueID, (ValueSource)account.ValueSource, name, cancellationToken) is null)
            {
                kept++;
            }
        }

        return kept;
    }

    /// <inheritdoc />
    public string? ErrorOf(Guid accountId) => _errors.TryGetValue(accountId, out var error) ? error : null;

    private async Task<string?> ValueAsync(Guid userId, Guid accountId, ValueSource source, string name, CancellationToken cancellationToken)
    {
        if (source != ValueSource.Obol)
        {
            return null;
        }

        if (!options.Value.ObolEnabled)
        {
            return _errors[accountId] = "off";
        }

        try
        {
            if (await obol.ValueAsync(name, cancellationToken) is { } value)
            {
                await accountRepository.SaveValueAsync(userId, accountId, clock.Today, value);
            }

            _errors.TryRemove(accountId, out _);
            return null;
        }
        catch (ObolException exception)
        {
            logger.LogWarning("Obol value of account {Account} failed: {Error}", accountId, exception.Message);
            return _errors[accountId] = exception.Message;
        }
    }
}
