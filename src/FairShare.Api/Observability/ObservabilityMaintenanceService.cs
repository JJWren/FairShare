using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Observability;

/// <summary>
/// Nightly (00:20 UTC) rollup of raw analytics into the Daily* aggregates, plus retention
/// purges: raw analytics 90 days, diagnostic logs 30 days, audit events 365 days. Catches up
/// on startup (watermark = max DailySiteStats.Day), rolls each day idempotently
/// (delete-then-insert in one transaction), and writes a DailySiteStats row even for
/// zero-traffic days so the watermark always advances.
/// </summary>
public class ObservabilityMaintenanceService(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<ObservabilityMaintenanceService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TimeProvider _time = time;
    private readonly ILogger<ObservabilityMaintenanceService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small start delay so migration/seeding settles first (mirrors RefreshTokenCleanupService).
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), _time, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Observability maintenance run failed; retrying at the next scheduled run.");
            }

            DateTime nowUtc = _time.GetUtcNow().UtcDateTime;
            TimeSpan delay = AnalyticsRollup.NextRunUtc(nowUtc) - nowUtc;

            try
            {
                await Task.Delay(delay, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One full maintenance pass. Public so tests can drive it directly.</summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        DateOnly today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        DateOnly? watermark = await db.DailySiteStats.OrderByDescending(s => s.Day).Select(s => (DateOnly?)s.Day).FirstOrDefaultAsync(ct);
        DateOnly? firstUnrolled = watermark?.AddDays(1) ?? await FindEarliestRawDayAsync(db, ct);

        if (firstUnrolled is DateOnly start)
        {
            for (DateOnly day = start; day < today; day = day.AddDays(1))
            {
                await RollUpDayAsync(db, day, ct);
            }
        }

        await PurgeAsync(db, today, ct);
    }

    private static async Task<DateOnly?> FindEarliestRawDayAsync(FairShareDbContext db, CancellationToken ct)
    {
        DateTime? earliestView = await db.PageViews.OrderBy(v => v.OccurredAtUtc).Select(v => (DateTime?)v.OccurredAtUtc).FirstOrDefaultAsync(ct);
        DateTime? earliestEvent = await db.AnalyticsEvents.OrderBy(e => e.OccurredAtUtc).Select(e => (DateTime?)e.OccurredAtUtc).FirstOrDefaultAsync(ct);

        DateTime? earliest = (earliestView, earliestEvent) switch
        {
            (null, null) => null,
            (null, DateTime e) => e,
            (DateTime v, null) => v,
            (DateTime v, DateTime e) => v < e ? v : e
        };

        return earliest is DateTime dt ? DateOnly.FromDateTime(dt) : null;
    }

    private async Task RollUpDayAsync(FairShareDbContext db, DateOnly day, CancellationToken ct)
    {
        (DateTime startUtc, DateTime endUtc) = AnalyticsRollup.DayBounds(day);

        List<PageView> views = await db.PageViews
            .Where(v => v.OccurredAtUtc >= startUtc && v.OccurredAtUtc < endUtc)
            .ToListAsync(ct);

        List<AnalyticsEvent> events = await db.AnalyticsEvents
            .Where(e => e.OccurredAtUtc >= startUtc && e.OccurredAtUtc < endUtc)
            .ToListAsync(ct);

        (DailySiteStat site, List<DailyRouteStat> routes, List<DailyReferrerStat> referrers, List<DailyEventStat> eventStats) =
            AnalyticsRollup.ComputeDay(day, views, events);

        // Idempotent per day: replace whatever a previous (possibly interrupted) run wrote.
        using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);

        await db.DailySiteStats.Where(s => s.Day == day).ExecuteDeleteAsync(ct);
        await db.DailyRouteStats.Where(s => s.Day == day).ExecuteDeleteAsync(ct);
        await db.DailyReferrerStats.Where(s => s.Day == day).ExecuteDeleteAsync(ct);
        await db.DailyEventStats.Where(s => s.Day == day).ExecuteDeleteAsync(ct);

        db.DailySiteStats.Add(site);
        db.DailyRouteStats.AddRange(routes);
        db.DailyReferrerStats.AddRange(referrers);
        db.DailyEventStats.AddRange(eventStats);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Rolled up analytics for {Day}: {Views} views, {Visitors} visitors.", day, site.Views, site.Visitors);
    }

    private async Task PurgeAsync(FairShareDbContext db, DateOnly today, CancellationToken ct)
    {
        DateTime nowUtc = _time.GetUtcNow().UtcDateTime;
        (DateTime todayStart, _) = AnalyticsRollup.DayBounds(today);

        // Raw analytics are only purged once rolled up (never delete unrolled days).
        DateTime rawCutoff = nowUtc - AnalyticsRollup.RawRetention;
        if (rawCutoff > todayStart)
        {
            rawCutoff = todayStart;
        }

        int views = await db.PageViews.Where(v => v.OccurredAtUtc < rawCutoff).ExecuteDeleteAsync(ct);
        int events = await db.AnalyticsEvents.Where(e => e.OccurredAtUtc < rawCutoff).ExecuteDeleteAsync(ct);
        int logs = await db.Logs.Where(l => l.OccurredAtUtc < nowUtc - AnalyticsRollup.LogRetention).ExecuteDeleteAsync(ct);
        int audits = await db.AuditEvents.Where(a => a.OccurredAtUtc < nowUtc - AnalyticsRollup.AuditRetention).ExecuteDeleteAsync(ct);

        if (views + events + logs + audits > 0)
        {
            _logger.LogInformation(
                "Retention purge removed {PageViews} page views, {Events} events, {Logs} log rows, {Audits} audit rows.",
                views, events, logs, audits);
        }
    }
}
