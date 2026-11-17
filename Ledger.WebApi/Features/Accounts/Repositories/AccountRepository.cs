using Ledger.Library.Enums;
using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Features.Accounts.Models.Entities;
using Ledger.WebApi.Features.Accounts.Services;

namespace Ledger.WebApi.Features.Accounts.Repositories;

/// <summary>
/// The accounts of a user (dbo.Account).
/// </summary>
internal interface IAccountRepository
{
    /// <summary>Lists the accounts in the person's order, each with its balance.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The accounts.</returns>
    Task<List<AccountRow>> ListAsync(Guid userId);

    /// <summary>Adds an account (no id: at the end of the list) or changes one.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account to change, or null for a new one.</param>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="openingBalance">What was in it before the first entry.</param>
    /// <returns>The account's id, or null when the account to change isn't this user's.</returns>
    Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance);

    /// <summary>Deletes an account; its entries stay, without an account.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="accountId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId, Guid accountId);

    /// <summary>Keeps the order the accounts were dragged into.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="order">The account ids, first to last.</param>
    /// <returns>A task.</returns>
    Task ReorderAsync(Guid userId, IReadOnlyList<Guid> order);
}

/// <summary>
/// The accounts in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Accounts).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class AccountRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IAccountRepository
{
    /// <inheritdoc />
    public Task<List<AccountRow>> ListAsync(Guid userId) => QueryAsync<AccountRow>("[dbo].[AccountList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance) =>
        ExecuteScalarAsync<Guid?>("[dbo].[AccountSave]", new
        {
            UserUniqueID = userId,
            AccountUniqueID = accountId,
            Name = name,
            Kind = (byte)kind,
            OpeningBalance = openingBalance,
        });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid accountId) =>
        ExecuteAsync("[dbo].[AccountDelete]", new { UserUniqueID = userId, AccountUniqueID = accountId });

    /// <inheritdoc />
    public Task ReorderAsync(Guid userId, IReadOnlyList<Guid> order) =>
        ExecuteAsync("[dbo].[AccountReorder]", new { UserUniqueID = userId, Order = Json(order) });
}

/// <summary>
/// The accounts in memory (demo mode).
/// </summary>
/// <param name="store">The memory tables.</param>
internal sealed class MemoryAccountRepository(MemoryStore store) : IAccountRepository
{
    /// <inheritdoc />
    public Task<List<AccountRow>> ListAsync(Guid userId)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            var rows = ledger.Accounts
                .OrderBy(static account => account.SortOrder)
                .Select(account => new AccountRow
                {
                    AccountUniqueID = account.AccountUniqueID,
                    Name = account.Name,
                    Kind = account.Kind,
                    OpeningBalance = account.OpeningBalance,
                    SortOrder = account.SortOrder,
                    Balance = account.OpeningBalance + Balances.Movement(account.AccountUniqueID, ledger.Entries),
                })
                .ToList();
            return Task.FromResult(rows);
        }
    }

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? accountId, string name, AccountKind kind, decimal openingBalance)
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
}
