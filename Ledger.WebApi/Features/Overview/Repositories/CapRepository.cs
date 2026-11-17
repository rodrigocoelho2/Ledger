using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Features.Overview.Models.Entities;

namespace Ledger.WebApi.Features.Overview.Repositories;

/// <summary>
/// The monthly spending caps of a user (dbo.MonthCap).
/// </summary>
internal interface ICapRepository
{
    /// <summary>Lists every month with a cap.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The caps, by the first day of the month.</returns>
    Task<Dictionary<DateOnly, decimal>> ListAsync(Guid userId);

    /// <summary>Sets the cap of a month, or removes it.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="monthStart">The first day of the month.</param>
    /// <param name="amount">The cap, or null for none.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, DateOnly monthStart, decimal? amount);
}

/// <summary>
/// The caps in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Caps).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class CapRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ICapRepository
{
    /// <inheritdoc />
    public async Task<Dictionary<DateOnly, decimal>> ListAsync(Guid userId) =>
        (await QueryAsync<CapRow>("[dbo].[CapList]", new { UserUniqueID = userId }))
            .ToDictionary(static row => DateOnly.FromDateTime(row.MonthStart), static row => row.Amount);

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, DateOnly monthStart, decimal? amount) =>
        ExecuteAsync("[dbo].[CapSave]", new { UserUniqueID = userId, MonthStart = Date(monthStart), Amount = amount });
}

/// <summary>
/// The caps in memory (demo mode).
/// </summary>
/// <param name="store">The memory tables.</param>
internal sealed class MemoryCapRepository(MemoryStore store) : ICapRepository
{
    /// <inheritdoc />
    public Task<Dictionary<DateOnly, decimal>> ListAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(new Dictionary<DateOnly, decimal>(store.For(userId).Caps));
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid userId, DateOnly monthStart, decimal? amount)
    {
        lock (store.Gate)
        {
            var caps = store.For(userId).Caps;
            if (amount is { } value)
            {
                caps[monthStart] = value;
            }
            else
            {
                caps.Remove(monthStart);
            }
        }

        return Task.CompletedTask;
    }
}
