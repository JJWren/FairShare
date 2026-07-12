using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Services;

// Refresh tokens are single-use (rotated) and guest sign-ins mint a row per click, so
// the table only shrinks if someone deletes the stale rows. Recently revoked tokens are
// kept for a retention window: a revoked-but-unexpired token showing up again is the
// replay signal, and deleting it too early would make a replay look like a random miss.
public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RevokedRetention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);

            using PeriodicTimer timer = new(Interval);

            do
            {
                try
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

                    int deleted = await DeleteStaleAsync(db, DateTime.UtcNow, stoppingToken);

                    if (deleted > 0)
                    {
                        logger.LogInformation("Refresh token cleanup removed {Count} stale rows.", deleted);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Refresh token cleanup pass failed; will retry next interval.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    public static async Task<int> DeleteStaleAsync(FairShareDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        // Precomputed: EF cannot translate DateTime arithmetic to SQL.
        DateTime revokedCutoff = nowUtc - RevokedRetention;

        return await db.RefreshTokens
            .Where(t => t.ExpiresUtc < nowUtc || (t.RevokedUtc != null && t.RevokedUtc < revokedCutoff))
            .ExecuteDeleteAsync(ct);
    }
}
