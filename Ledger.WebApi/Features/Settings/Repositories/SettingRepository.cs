using Ledger.WebApi.Extensions.Data;

namespace Ledger.WebApi.Features.Settings.Repositories;

/// <summary>
/// What a user chose in Settings (dbo.Setting). It is read together with the user (dbo.UserGet).
/// </summary>
internal interface ISettingRepository
{
    /// <summary>Keeps the language, which optional modules are on and where the months begin.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="language">"en" or "pt".</param>
    /// <param name="rentalEnabled">Whether the short-term rental module is on.</param>
    /// <param name="flipsEnabled">Whether the buy-and-sell module is on.</param>
    /// <param name="payDay">The day of the month the salary is due (null: calendar months).</param>
    /// <param name="payer">A word of the salary's bank description (null: none).</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled, byte? payDay = null, string? payer = null);
}

/// <summary>
/// The settings in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Settings).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class SettingRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ISettingRepository
{
    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled, byte? payDay = null, string? payer = null) =>
        ExecuteAsync("[dbo].[SettingSave]", new { UserUniqueID = userId, Language = language, RentalEnabled = rentalEnabled, FlipsEnabled = flipsEnabled, PayDay = payDay, Payer = payer });
}

/// <summary>
/// The settings in memory (demo mode): kept on the user itself.
/// </summary>
/// <param name="store">The memory tables.</param>
internal sealed class MemorySettingRepository(MemoryStore store) : ISettingRepository
{
    /// <inheritdoc />
    public Task SaveAsync(Guid userId, string language, bool rentalEnabled, bool flipsEnabled, byte? payDay = null, string? payer = null)
    {
        lock (store.Gate)
        {
            if (store.Users.FirstOrDefault(user => user.UserUniqueID == userId) is { } user)
            {
                user.Language = language;
                user.RentalEnabled = rentalEnabled;
                user.FlipsEnabled = flipsEnabled;
                user.PayDay = payDay;
                user.Payer = payer;
            }
        }

        return Task.CompletedTask;
    }
}
