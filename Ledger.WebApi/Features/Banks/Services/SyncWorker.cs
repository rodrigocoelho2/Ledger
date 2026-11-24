using Ledger.WebApi.Features.Accounts.Services;
using Microsoft.Extensions.Options;

namespace Ledger.WebApi.Features.Banks.Services;

/// <summary>
/// Keeps the outside world in step on its own: values from Obol every <see cref="LedgerOptions.ObolMinutes"/>, and each
/// connected bank account when its turn comes (every <see cref="LedgerOptions.BankSyncHours"/>).
/// </summary>
/// <param name="bankService">Bank syncs.</param>
/// <param name="valuationService">Values from Obol.</param>
/// <param name="options">The app's options.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed class SyncWorker(
    IBankService bankService,
    IValuationService valuationService,
    IOptions<LedgerOptions> options,
    TimeProvider timeProvider,
    ILogger<SyncWorker> logger) : BackgroundService
{
    /// <summary>How often the worker looks.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.BankEnabled && !settings.ObolEnabled)
        {
            return;
        }

        // Let the app finish starting (and the database wake up) first.
        await Task.Delay(TimeSpan.FromSeconds(20), timeProvider, stoppingToken);
        var nextObol = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(Tick, timeProvider);
        do
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                if (settings.ObolEnabled && now >= nextObol)
                {
                    await valuationService.RefreshAllAsync(stoppingToken);
                    nextObol = now.AddMinutes(Math.Max(5, settings.ObolMinutes));
                }

                if (settings.BankEnabled && await bankService.SyncDueAsync(stoppingToken) is var synced and > 0)
                {
                    logger.LogInformation("Synced {Count} bank account(s)", synced);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The worker must outlive a bad round (database restarting, network down).
            catch (Exception exception)
#pragma warning restore CA1031
            {
                logger.LogWarning(exception, "Sync round failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
