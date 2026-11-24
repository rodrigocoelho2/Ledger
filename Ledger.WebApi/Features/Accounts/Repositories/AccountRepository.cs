using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Extensions.Time;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Accounts.Services;

namespace Ledger.WebApi.Features.Accounts.Repositories;

/// <summary>
/// The accounts of a user (dbo.Account), and the values of the ones valued elsewhere (dbo.AccountValue).
/// </summary>
internal interface IAccountRepository
{
    /// <summary>Lists the accounts in the person's order, each with its balance today (instalments still to come left out).</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The accounts.</returns>
    Task<List<AccountRow>> ListAsync(Guid userId);

    /// <summary>Adds an account or changes one.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account to change, or null for a new one.</param>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="openingBalance">What was in it before the first entry.</param>
    /// <param name="valueSource">Where its value comes from (null: its entries).</param>
    /// <param name="valueRef">The user name at that source (null: the same as in Ledger).</param>
    /// <returns>The account's id, or null when the account to change isn't the person's.</returns>
    Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance, ValueSource? valueSource = null, string? valueRef = null);

    /// <summary>Deletes an account; its entries stay, with the account emptied.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account.</param>
    /// <returns>A task that completes when it is deleted.</returns>
    Task DeleteAsync(Guid userId, Guid accountId);

    /// <summary>Keeps the order the person dragged the accounts into.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="order">The account ids, first to last.</param>
    /// <returns>A task that completes when it is kept.</returns>
    Task ReorderAsync(Guid userId, IReadOnlyList<Guid> order);

    /// <summary>Keeps what an account valued elsewhere is worth on a day.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="day">The day.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes when it is kept.</returns>
    Task SaveValueAsync(Guid userId, Guid accountId, DateOnly day, decimal value);

    /// <summary>The values kept from a day on, plus the last one before it, oldest first.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="from">The first day.</param>
    /// <returns>The values.</returns>
    Task<List<AccountValueRow>> ValuesAsync(Guid userId, DateOnly from);

    /// <summary>Every account of every person whose value comes from elsewhere.</summary>
    /// <returns>The accounts.</returns>
    Task<List<ValuedAccountRow>> ValuedAllAsync();
}

/// <summary>
/// The accounts in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Accounts).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
/// <param name="clock">Today, for the balances.</param>
internal sealed class AccountRepository(ISqlConnectionFactory connectionFactory, IAppClock clock) : RepositoryBase(connectionFactory), IAccountRepository
{
    /// <inheritdoc />
    public async Task<List<AccountRow>> ListAsync(Guid userId) =>
        [.. (await QueryAsync<AccountRow>("[dbo].[AccountList]", new { UserUniqueID = userId, AsOf = Date(clock.Today) })).Select(static row => row.Settle())];

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance, ValueSource? valueSource = null, string? valueRef = null) =>
        ExecuteScalarAsync<Guid?>("[dbo].[AccountSave]", new
        {
            UserUniqueID = userId,
            AccountUniqueID = accountId,
            Name = name,
            Kind = (byte)kind,
            OpeningBalance = openingBalance,
            ValueSource = (byte?)valueSource,
            ValueRef = valueRef,
        });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid accountId) =>
        ExecuteAsync("[dbo].[AccountDelete]", new { UserUniqueID = userId, AccountUniqueID = accountId });

    /// <inheritdoc />
    public Task ReorderAsync(Guid userId, IReadOnlyList<Guid> order) =>
        ExecuteAsync("[dbo].[AccountReorder]", new { UserUniqueID = userId, Order = Json(order) });

    /// <inheritdoc />
    public Task SaveValueAsync(Guid userId, Guid accountId, DateOnly day, decimal value) =>
        ExecuteAsync("[dbo].[AccountValueSave]", new { UserUniqueID = userId, AccountUniqueID = accountId, ValueDate = Date(day), Value = value });

    /// <inheritdoc />
    public Task<List<AccountValueRow>> ValuesAsync(Guid userId, DateOnly from) =>
        QueryAsync<AccountValueRow>("[dbo].[AccountValueList]", new { UserUniqueID = userId, From = Date(from) });

    /// <inheritdoc />
    public Task<List<ValuedAccountRow>> ValuedAllAsync() => QueryAsync<ValuedAccountRow>("[dbo].[AccountValuedAll]", new { });
}

/// <summary>
/// The accounts in memory (demo mode).
/// </summary>
/// <param name="store">The memory tables.</param>
/// <param name="clock">Today, for the balances.</param>
internal sealed class MemoryAccountRepository(MemoryStore store, IAppClock clock) : IAccountRepository
{
    /// <inheritdoc />
    public Task<List<AccountRow>> ListAsync(Guid userId)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            var today = clock.Today;
            var happened = ledger.Entries.Where(entry => entry.Day <= today).ToList();
            var rows = ledger.Accounts
                .OrderBy(static account => account.SortOrder)
                .Select(account =>
                {
                    var last = ledger.Values.Where(value => value.Key.Account == account.AccountUniqueID).OrderByDescending(static value => value.Key.Day).Select(static value => (KeyValuePair<(Guid, DateOnly), decimal>?)value).FirstOrDefault();
                    var bank = ledger.BankLinks.FirstOrDefault(link => link.AccountUniqueID == account.AccountUniqueID);
                    return new AccountRow
                    {
                        AccountUniqueID = account.AccountUniqueID,
                        Name = account.Name,
                        Kind = account.Kind,
                        OpeningBalance = account.OpeningBalance,
                        SortOrder = account.SortOrder,
                        ValueSource = account.ValueSource,
                        ValueRef = account.ValueRef,
                        Value = last?.Value,
                        ValueDate = last?.Key.Item2.ToDateTime(TimeOnly.MinValue),
                        BankName = bank?.BankName,
                        BankSyncedAt = bank?.LastSyncAt,
                        BankBalance = bank?.BankBalance,
                        Movement = Balances.Movement(account.AccountUniqueID, happened),
                    }.Settle();
                })
                .ToList();
            return Task.FromResult(rows);
        }
    }

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance, ValueSource? valueSource = null, string? valueRef = null)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            if (accountId is null)
            {
                var row = new AccountRow
                {
                    AccountUniqueID = Guid.NewGuid(),
                    Name = name,
                    Kind = (byte)kind,
                    OpeningBalance = openingBalance,
                    SortOrder = ledger.Accounts.Count == 0 ? 1 : ledger.Accounts.Max(static account => account.SortOrder) + 1,
                    ValueSource = (byte?)valueSource,
                    ValueRef = valueRef,
                };
                ledger.Accounts.Add(row);
                return Task.FromResult<Guid?>(row.AccountUniqueID);
            }

            var existing = ledger.Accounts.FirstOrDefault(account => account.AccountUniqueID == accountId);
            if (existing is null)
            {
                return Task.FromResult<Guid?>(null);
            }

            existing.Name = name;
            existing.Kind = (byte)kind;
            existing.OpeningBalance = openingBalance;
            existing.ValueSource = (byte?)valueSource;
            existing.ValueRef = valueRef;
            return Task.FromResult<Guid?>(existing.AccountUniqueID);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid accountId)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            if (ledger.Accounts.RemoveAll(account => account.AccountUniqueID == accountId) > 0)
            {
                foreach (var entry in ledger.Entries)
                {
                    if (entry.AccountUniqueID == accountId)
                    {
                        entry.AccountUniqueID = null;
                    }

                    if (entry.ToAccountUniqueID == accountId)
                    {
                        entry.ToAccountUniqueID = null;
                    }
                }

                foreach (var key in ledger.Values.Keys.Where(key => key.Account == accountId).ToList())
                {
                    ledger.Values.Remove(key);
                }

                foreach (var link in ledger.BankLinks.Where(link => link.AccountUniqueID == accountId))
                {
                    link.AccountUniqueID = null;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReorderAsync(Guid userId, IReadOnlyList<Guid> order)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            for (var index = 0; index < order.Count; index++)
            {
                if (ledger.Accounts.FirstOrDefault(account => account.AccountUniqueID == order[index]) is { } account)
                {
                    account.SortOrder = index + 1;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveValueAsync(Guid userId, Guid accountId, DateOnly day, decimal value)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            if (ledger.Accounts.Any(account => account.AccountUniqueID == accountId))
            {
                ledger.Values[(accountId, day)] = value;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<AccountValueRow>> ValuesAsync(Guid userId, DateOnly from)
    {
        lock (store.Gate)
        {
            var values = store.For(userId).Values
                .GroupBy(static value => value.Key.Account)
                .SelectMany(account =>
                {
                    var ordered = account.OrderBy(static value => value.Key.Day).ToList();
                    var before = ordered.Where(value => value.Key.Day < from).TakeLast(1);
                    return before.Concat(ordered.Where(value => value.Key.Day >= from));
                })
                .OrderBy(static value => value.Key.Day)
                .Select(static value => new AccountValueRow { AccountUniqueID = value.Key.Account, ValueDate = value.Key.Day.ToDateTime(TimeOnly.MinValue), Value = value.Value })
                .ToList();
            return Task.FromResult(values);
        }
    }

    /// <inheritdoc />
    public Task<List<ValuedAccountRow>> ValuedAllAsync()
    {
        lock (store.Gate)
        {
            var rows = store.Ledgers
                .SelectMany(ledger => ledger.Value.Accounts
                    .Where(static account => account.ValueSource is not null)
                    .Select(account => new ValuedAccountRow
                    {
                        UserUniqueID = ledger.Key,
                        Username = store.Users.FirstOrDefault(user => user.UserUniqueID == ledger.Key)?.Username ?? string.Empty,
                        AccountUniqueID = account.AccountUniqueID,
                        ValueSource = account.ValueSource!.Value,
                        ValueRef = account.ValueRef,
                    }))
                .ToList();
            return Task.FromResult(rows);
        }
    }
}
