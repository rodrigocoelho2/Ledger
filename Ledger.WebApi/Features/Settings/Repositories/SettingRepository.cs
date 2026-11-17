using Ledger.WebApi.Extensions.Data;

namespace Ledger.WebApi.Features.Settings.Repositories;

/// <summary>
/// What a user chose in Settings (dbo.Setting). It is read together with the user (dbo.UserGet).
/// </summary>
internal interface ISettingRepository
{
    /// <summary>Keeps the language and which optional modules are on.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="language">"en" or "pt".</param>
    /// <param name="rentalEnabled">Whether the short-term rental module is on.</param>
    /// <param name="flipsEnabled">Whether the buy-and-sell module is on.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled);
}

/// <summary>
/// The settings in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Settings).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class SettingRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISettingRepository
{
    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled) =>
        ExecuteAsync("[dbo].[SettingSave]", new { UserUniqueID = userId, Language = language, RentalEnabled = rentalEnabled, FlipsEnabled = flipsEnabled });
}

/// <summary>
/// The settings in memory (demo mode): kept on the user itself.
/// </summary>
/// <param name="store">The memory tables.</param>
internal sealed class MemorySettingRepository(MemoryStore store) : ISettingRepository
{
    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled)
    {
        lock (store.Gate)
        {
            if (store.Users.FirstOrDefault(user => user.UserUniqueID == userId) is { } user)
            {
                user.Language = language;
                user.RentalEnabled = rentalEnabled;
                user.FlipsEnabled = flipsEnabled;
            }
        }

        return Task.CompletedTask;
    }
}
