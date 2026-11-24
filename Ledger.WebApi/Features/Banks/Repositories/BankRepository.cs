using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Features.Banks.Models.Entities;

namespace Ledger.WebApi.Features.Banks.Repositories;

/// <summary>
/// The bank accounts people connected (dbo.BankLink).
/// </summary>
internal interface IBankRepository
{
    /// <summary>The bank accounts a person connected, oldest first.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The links.</returns>
    Task<List<BankLinkRow>> ListAsync(Guid userId);

    /// <summary>Every link that has a Ledger account to go into, of every person.</summary>
    /// <returns>The links.</returns>
    Task<List<BankLinkRow>> AllAsync();

    /// <summary>Keeps a bank account just authorised (the same account again keeps its Ledger account).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="link">What the bank said about it.</param>
    /// <returns>The link's id, or null when the user is gone.</returns>
    Task<Guid?> SaveAsync(Guid userId, BankLinkRow link);

    /// <summary>Says which Ledger account a bank account goes into, and from which day.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="linkId">The link.</param>
    /// <param name="accountId">The Ledger account (null: stop syncing).</param>
    /// <param name="importFrom">The first day to bring.</param>
    /// <returns>True when it was the person's link and account.</returns>
    Task<bool> AssignAsync(Guid userId, Guid linkId, Guid? accountId, DateOnly? importFrom);

    /// <summary>Keeps how a sync went.</summary>
    /// <param name="linkId">The link.</param>
    /// <param name="at">When (UTC).</param>
    /// <param name="error">Why it failed (null: it worked).</param>
    /// <param name="balance">The balance the bank reported.</param>
    /// <returns>A task that completes when it is kept.</returns>
    Task SyncedAsync(Guid linkId, DateTime at, string? error, decimal? balance);

    /// <summary>Disconnects a bank account; its entries stay.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="linkId">The link.</param>
    /// <returns>True when it was the person's.</returns>
    Task<bool> DeleteAsync(Guid userId, Guid linkId);
}

/// <summary>
/// The links in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Banks).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class BankRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IBankRepository
{
    /// <inheritdoc />
    public Task<List<BankLinkRow>> ListAsync(Guid userId) => QueryAsync<BankLinkRow>("[dbo].[BankLinkList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<List<BankLinkRow>> AllAsync() => QueryAsync<BankLinkRow>("[dbo].[BankLinkAll]", new { });

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, BankLinkRow link) =>
        ExecuteScalarAsync<Guid?>("[dbo].[BankLinkSave]", new
        {
            UserUniqueID = userId,
            link.SessionID,
            link.AccountUid,
            link.BankName,
            link.BankCountry,
            link.AccountLabel,
            link.IbanTail,
            link.ValidUntil,
        });

    /// <inheritdoc />
    public async Task<bool> AssignAsync(Guid userId, Guid linkId, Guid? accountId, DateOnly? importFrom) =>
        await ExecuteScalarAsync<int>("[dbo].[BankLinkAssign]", new
        {
            UserUniqueID = userId,
            BankLinkUniqueID = linkId,
            AccountUniqueID = accountId,
            ImportFrom = importFrom is { } day ? Date(day) : (DateTime?)null,
        }) > 0;

    /// <inheritdoc />
    public Task SyncedAsync(Guid linkId, DateTime at, string? error, decimal? balance) =>
        ExecuteAsync("[dbo].[BankLinkSynced]", new { BankLinkUniqueID = linkId, LastSyncAt = at, LastError = error, BankBalance = balance });

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, Guid linkId) =>
        await ExecuteScalarAsync<int>("[dbo].[BankLinkDelete]", new { UserUniqueID = userId, BankLinkUniqueID = linkId }) > 0;
}

/// <summary>
/// The links in memory (demo mode and tests).
/// </summary>
/// <param name="store">The memory tables.</param>
/// <param name="timeProvider">The clock, for when a link was made.</param>
internal sealed class MemoryBankRepository(MemoryStore store, TimeProvider timeProvider) : IBankRepository
{
    /// <inheritdoc />
    public Task<List<BankLinkRow>> ListAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.For(userId).BankLinks.Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task<List<BankLinkRow>> AllAsync()
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Ledgers.Values.SelectMany(static ledger => ledger.BankLinks).Where(static link => link.AccountUniqueID is not null).Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, BankLinkRow link)
    {
        lock (store.Gate)
        {
            var links = store.For(userId).BankLinks;
            var existing = links.FirstOrDefault(row => row.AccountUid == link.AccountUid
                || (link.IbanTail is not null && row.IbanTail == link.IbanTail && row.BankName == link.BankName));
            if (existing is null)
            {
                existing = Copy(link);
                existing.BankLinkUniqueID = Guid.NewGuid();
                existing.UserUniqueID = userId;
                existing.AccountUniqueID = null;
                existing.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
                links.Add(existing);
            }
            else
            {
                existing.SessionID = link.SessionID;
                existing.AccountUid = link.AccountUid;
                existing.AccountLabel = link.AccountLabel;
                existing.ValidUntil = link.ValidUntil;
                existing.LastError = null;
            }

            return Task.FromResult<Guid?>(existing.BankLinkUniqueID);
        }
    }

    /// <inheritdoc />
    public Task<bool> AssignAsync(Guid userId, Guid linkId, Guid? accountId, DateOnly? importFrom)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            var link = ledger.BankLinks.FirstOrDefault(row => row.BankLinkUniqueID == linkId);
            if (link is null || (accountId is not null && ledger.Accounts.All(account => account.AccountUniqueID != accountId)))
            {
                return Task.FromResult(false);
            }

            link.AccountUniqueID = accountId;
            link.ImportFrom = importFrom?.ToDateTime(TimeOnly.MinValue);
            link.LastSyncAt = null;
            link.LastError = null;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task SyncedAsync(Guid linkId, DateTime at, string? error, decimal? balance)
    {
        lock (store.Gate)
        {
            if (store.Ledgers.Values.SelectMany(static ledger => ledger.BankLinks).FirstOrDefault(row => row.BankLinkUniqueID == linkId) is { } link)
            {
                link.LastSyncAt = error is null ? at : link.LastSyncAt;
                link.LastError = error;
                link.BankBalance = balance ?? link.BankBalance;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid userId, Guid linkId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.For(userId).BankLinks.RemoveAll(row => row.BankLinkUniqueID == linkId) > 0);
        }
    }

    private static BankLinkRow Copy(BankLinkRow row) => new()
    {
        BankLinkUniqueID = row.BankLinkUniqueID,
        UserUniqueID = row.UserUniqueID,
        AccountUniqueID = row.AccountUniqueID,
        SessionID = row.SessionID,
        AccountUid = row.AccountUid,
        BankName = row.BankName,
        BankCountry = row.BankCountry,
        AccountLabel = row.AccountLabel,
        IbanTail = row.IbanTail,
        ValidUntil = row.ValidUntil,
        ImportFrom = row.ImportFrom,
        LastSyncAt = row.LastSyncAt,
        LastError = row.LastError,
        BankBalance = row.BankBalance,
        CreatedAt = row.CreatedAt,
    };
}
