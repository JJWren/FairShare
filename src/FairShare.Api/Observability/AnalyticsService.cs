using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Persistence;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Observability;

public interface IAnalyticsService
{
    /// <summary>Records a page view if every capture rule passes. Best-effort, never throws.</summary>
    Task RecordPageViewAsync(HttpContext context, string? path, string? referrer, CancellationToken ct = default);

    /// <summary>Records a content-free event if the capture rules pass. Best-effort, never throws.</summary>
    Task RecordEventAsync(HttpContext context, string name, string? target, CancellationToken ct = default);

    Task<StatsSummaryResponse> GetSummaryAsync(int? days, CancellationToken ct = default);
    Task<PagedResult<PageStatRow>> GetTopPagesAsync(int? days, int page, int pageSize, string sort, bool descending, CancellationToken ct = default);
    Task<List<ReferrerStatRow>> GetTopReferrersAsync(int? days, int take, CancellationToken ct = default);
    Task<List<EventStatRow>> GetEventsAsync(int? days, CancellationToken ct = default);
}

public class AnalyticsService(
    FairShareDbContext db,
    AnalyticsSecretProvider secretProvider,
    TimeProvider time,
    ILogger<AnalyticsService> logger) : IAnalyticsService
{
    private readonly FairShareDbContext _db = db;
    private readonly AnalyticsSecretProvider _secretProvider = secretProvider;
    private readonly TimeProvider _time = time;
    private readonly ILogger<AnalyticsService> _logger = logger;

    public async Task RecordPageViewAsync(HttpContext context, string? path, string? referrer, CancellationToken ct = default)
    {
        try
        {
            if (!ShouldRecord(context, out string ip, out string userAgent)
                || !AnalyticsRules.TryNormalizePath(path, out string normalizedPath))
            {
                return;
            }

            DateTime nowUtc = _time.GetUtcNow().UtcDateTime;
            byte[] secret = await _secretProvider.GetSecretAsync(ct);

            _db.PageViews.Add(new PageView
            {
                Path = normalizedPath,
                ReferrerHost = AnalyticsRules.NormalizeReferrer(referrer, context.Request.Host.Host),
                VisitorKey = VisitorKey.Compute(secret, DateOnly.FromDateTime(nowUtc), ip, userAgent),
                OccurredAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Telemetry must never fail a request (NFR-4).
            _logger.LogWarning(ex, "Page-view recording failed.");
        }
    }

    public async Task RecordEventAsync(HttpContext context, string name, string? target, CancellationToken ct = default)
    {
        try
        {
            if (!ShouldRecord(context, out string ip, out string userAgent))
            {
                return;
            }

            DateTime nowUtc = _time.GetUtcNow().UtcDateTime;
            byte[] secret = await _secretProvider.GetSecretAsync(ct);

            _db.AnalyticsEvents.Add(new AnalyticsEvent
            {
                Name = name,
                Target = target,
                VisitorKey = VisitorKey.Compute(secret, DateOnly.FromDateTime(nowUtc), ip, userAgent),
                OccurredAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Event names can arrive via the client beacon; stripping line breaks keeps a
            // crafted name from forging extra lines in the admin-readable log store.
            _logger.LogWarning(ex, "Analytics event recording failed for {EventName}.", name.ReplaceLineEndings(" "));
        }
    }

    private static bool ShouldRecord(HttpContext context, out string ip, out string userAgent)
    {
        ip = string.Empty;
        userAgent = context.Request.Headers.UserAgent.ToString();

        if (AnalyticsRules.OptedOut(context.Request.Headers)
            || AnalyticsRules.IsBot(userAgent)
            || context.User.IsInRole("Admin"))
        {
            return false;
        }

        // First X-Forwarded-For hop when the proxy supplies one, else the peer. Only the
        // visitor hash consumes this, so a spoofed header can at worst split or merge
        // daily-visitor counts - the rate limiter's peer-IP keying is deliberately untouched.
        string forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        ip = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return true;
    }

    // ---- Queries ------------------------------------------------------------------

    public async Task<StatsSummaryResponse> GetSummaryAsync(int? days, CancellationToken ct = default)
    {
        (DateOnly from, DateOnly today) = await ResolveRangeAsync(days, ct);

        List<DailySiteStat> siteStats = await _db.DailySiteStats
            .Where(s => s.Day >= from && s.Day < today)
            .ToListAsync(ct);

        (DateTime todayStart, _) = AnalyticsRollup.DayBounds(today);

        List<PageView> todayViews = await _db.PageViews
            .Where(v => v.OccurredAtUtc >= todayStart)
            .ToListAsync(ct);

        Dictionary<(string Name, string? Target), long> eventCounts = await GetEventCountsAsync(from, today, ct);

        DateOnly? firstDay = await _db.DailySiteStats.OrderBy(s => s.Day).Select(s => (DateOnly?)s.Day).FirstOrDefaultAsync(ct);
        if (firstDay is null && (todayViews.Count > 0 || eventCounts.Count > 0))
        {
            firstDay = today;
        }

        return new StatsSummaryResponse
        {
            PageViews = siteStats.Sum(s => s.Views) + todayViews.Count,
            DailyVisitors = siteStats.Sum(s => s.Visitors) + todayViews.Select(v => v.VisitorKey).Distinct().Count(),
            CalculationsCompleted = SumEvent(eventCounts, AnalyticsEventNames.CalculationCompleted),
            GatedHits = SumEvent(eventCounts, AnalyticsEventNames.GatedHit),
            DonateClicks = SumEvent(eventCounts, AnalyticsEventNames.DonateClick),
            FirstDay = firstDay
        };
    }

    public async Task<PagedResult<PageStatRow>> GetTopPagesAsync(int? days, int page, int pageSize, string sort, bool descending, CancellationToken ct = default)
    {
        (DateOnly from, DateOnly today) = await ResolveRangeAsync(days, ct);

        List<DailyRouteStat> rolled = await _db.DailyRouteStats
            .Where(s => s.Day >= from && s.Day < today)
            .ToListAsync(ct);

        (DateTime todayStart, _) = AnalyticsRollup.DayBounds(today);
        List<PageView> todayViews = await _db.PageViews
            .Where(v => v.OccurredAtUtc >= todayStart)
            .ToListAsync(ct);

        Dictionary<string, PageStatRow> byPath = new(StringComparer.Ordinal);

        foreach (DailyRouteStat stat in rolled)
        {
            PageStatRow row = GetOrAdd(byPath, stat.Path);
            row.Views += stat.Views;
            row.Visitors += stat.Visitors;
        }

        foreach (IGrouping<string, PageView> group in todayViews.GroupBy(v => v.Path))
        {
            PageStatRow row = GetOrAdd(byPath, group.Key);
            row.Views += group.Count();
            row.Visitors += group.Select(v => v.VisitorKey).Distinct().Count();
        }

        IEnumerable<PageStatRow> ordered = sort.ToLowerInvariant() switch
        {
            "path" => descending ? byPath.Values.OrderByDescending(r => r.Path) : byPath.Values.OrderBy(r => r.Path),
            "visitors" => descending ? byPath.Values.OrderByDescending(r => r.Visitors) : byPath.Values.OrderBy(r => r.Visitors),
            _ => descending ? byPath.Values.OrderByDescending(r => r.Views) : byPath.Values.OrderBy(r => r.Views)
        };

        List<PageStatRow> all = ordered.ToList();

        return new PagedResult<PageStatRow>
        {
            Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = all.Count
        };

        static PageStatRow GetOrAdd(Dictionary<string, PageStatRow> map, string path)
        {
            if (!map.TryGetValue(path, out PageStatRow? row))
            {
                row = new PageStatRow { Path = path };
                map[path] = row;
            }

            return row;
        }
    }

    public async Task<List<ReferrerStatRow>> GetTopReferrersAsync(int? days, int take, CancellationToken ct = default)
    {
        (DateOnly from, DateOnly today) = await ResolveRangeAsync(days, ct);

        List<DailyReferrerStat> rolled = await _db.DailyReferrerStats
            .Where(s => s.Day >= from && s.Day < today)
            .ToListAsync(ct);

        (DateTime todayStart, _) = AnalyticsRollup.DayBounds(today);
        List<string> todayHosts = await _db.PageViews
            .Where(v => v.OccurredAtUtc >= todayStart && v.ReferrerHost != null)
            .Select(v => v.ReferrerHost!)
            .ToListAsync(ct);

        return rolled
            .Select(s => (Host: s.ReferrerHost, Views: s.Views))
            .Concat(todayHosts.GroupBy(h => h).Select(g => (Host: g.Key, Views: (long)g.Count())))
            .GroupBy(t => t.Host)
            .Select(g => new ReferrerStatRow { ReferrerHost = g.Key, Views = g.Sum(t => t.Views) })
            .OrderByDescending(r => r.Views)
            .Take(take)
            .ToList();
    }

    public async Task<List<EventStatRow>> GetEventsAsync(int? days, CancellationToken ct = default)
    {
        (DateOnly from, DateOnly today) = await ResolveRangeAsync(days, ct);
        Dictionary<(string Name, string? Target), long> counts = await GetEventCountsAsync(from, today, ct);

        return counts
            .Select(kvp => new EventStatRow { Name = kvp.Key.Name, Target = kvp.Key.Target, Count = kvp.Value })
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    private async Task<Dictionary<(string Name, string? Target), long>> GetEventCountsAsync(DateOnly from, DateOnly today, CancellationToken ct)
    {
        List<DailyEventStat> rolled = await _db.DailyEventStats
            .Where(s => s.Day >= from && s.Day < today)
            .ToListAsync(ct);

        (DateTime todayStart, _) = AnalyticsRollup.DayBounds(today);
        List<AnalyticsEvent> todayEvents = await _db.AnalyticsEvents
            .Where(e => e.OccurredAtUtc >= todayStart)
            .ToListAsync(ct);

        Dictionary<(string, string?), long> counts = [];

        foreach (DailyEventStat stat in rolled)
        {
            (string, string?) key = (stat.Name, stat.Target);
            counts[key] = counts.GetValueOrDefault(key) + stat.Count;
        }

        foreach (AnalyticsEvent evt in todayEvents)
        {
            (string, string?) key = (evt.Name, evt.Target);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static long SumEvent(Dictionary<(string Name, string? Target), long> counts, string name) =>
        counts.Where(kvp => kvp.Key.Name == name).Sum(kvp => kvp.Value);

    /// <summary>Inclusive from-day and today (exclusive upper bound for rollups; live otherwise).</summary>
    private async Task<(DateOnly From, DateOnly Today)> ResolveRangeAsync(int? days, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (days is int d && d > 0)
        {
            return (today.AddDays(-(d - 1)), today);
        }

        // All time: from the earliest rolled-up day (or today when nothing is rolled yet).
        DateOnly? first = await _db.DailySiteStats.OrderBy(s => s.Day).Select(s => (DateOnly?)s.Day).FirstOrDefaultAsync(ct);
        return (first ?? today, today);
    }
}
