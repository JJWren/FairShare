using System;
using System.Collections.Generic;
using System.Linq;

namespace FairShare.Api.Observability;

/// <summary>Pure aggregation of one UTC day's raw rows into the four Daily* shapes.</summary>
public static class AnalyticsRollup
{
    public static readonly TimeSpan RawRetention = TimeSpan.FromDays(90);
    public static readonly TimeSpan LogRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan AuditRetention = TimeSpan.FromDays(365);

    /// <summary>00:20 UTC: 20 minutes of slack after midnight for clock skew.</summary>
    public static readonly TimeSpan DailyRunTime = new(0, 20, 0);

    public static (DailySiteStat Site, List<DailyRouteStat> Routes, List<DailyReferrerStat> Referrers, List<DailyEventStat> Events)
        ComputeDay(DateOnly day, IReadOnlyCollection<PageView> pageViews, IReadOnlyCollection<AnalyticsEvent> events)
    {
        DailySiteStat site = new()
        {
            Day = day,
            Views = pageViews.Count,
            Visitors = pageViews.Select(v => v.VisitorKey).Distinct().Count()
        };

        List<DailyRouteStat> routes = pageViews
            .GroupBy(v => v.Path)
            .Select(g => new DailyRouteStat
            {
                Day = day,
                Path = g.Key,
                Views = g.Count(),
                Visitors = g.Select(v => v.VisitorKey).Distinct().Count()
            })
            .ToList();

        List<DailyReferrerStat> referrers = pageViews
            .Where(v => v.ReferrerHost is not null)
            .GroupBy(v => v.ReferrerHost!)
            .Select(g => new DailyReferrerStat { Day = day, ReferrerHost = g.Key, Views = g.Count() })
            .ToList();

        List<DailyEventStat> eventStats = events
            .GroupBy(e => (e.Name, e.Target))
            .Select(g => new DailyEventStat { Day = day, Name = g.Key.Name, Target = g.Key.Target, Count = g.Count() })
            .ToList();

        return (site, routes, referrers, eventStats);
    }

    /// <summary>UTC start/end of a day, kinds pinned so timestamptz-style engines stay happy.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayBounds(DateOnly day)
    {
        DateTime start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return (start, start.AddDays(1));
    }

    public static DateTime NextRunUtc(DateTime nowUtc)
    {
        DateTime todayRun = nowUtc.Date + DailyRunTime;
        return nowUtc < todayRun ? todayRun : todayRun.AddDays(1);
    }
}
