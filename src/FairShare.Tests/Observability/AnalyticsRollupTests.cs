using FairShare.Api.Observability;

namespace FairShare.Tests.Observability;

public class AnalyticsRollupTests
{
    private static readonly DateOnly Day = new(2026, 8, 19);

    private static PageView View(string path, string visitor, string? referrer = null) =>
        new() { Path = path, VisitorKey = visitor, ReferrerHost = referrer, OccurredAtUtc = Day.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc) };

    private static AnalyticsEvent Event(string name, string? target, string visitor) =>
        new() { Name = name, Target = target, VisitorKey = visitor, OccurredAtUtc = Day.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc) };

    [Fact]
    public void ComputeDay_AggregatesViewsVisitorsRoutesReferrersAndEvents()
    {
        List<PageView> views =
        [
            View("/", "a", "www.google.com"),
            View("/", "a"),
            View("/states/al/cs42", "a"),
            View("/states/al/cs42", "b", "www.google.com"),
            View("/states/al/cs42", "b", "duckduckgo.com")
        ];

        List<AnalyticsEvent> events =
        [
            Event("calculation-completed", "al/cs42", "a"),
            Event("calculation-completed", "al/cs42", "b"),
            Event("gated-hit", "profiles", "b")
        ];

        (DailySiteStat site, List<DailyRouteStat> routes, List<DailyReferrerStat> referrers, List<DailyEventStat> eventStats) =
            AnalyticsRollup.ComputeDay(Day, views, events);

        Assert.Equal(5, site.Views);
        Assert.Equal(2, site.Visitors);

        DailyRouteStat calc = Assert.Single(routes, r => r.Path == "/states/al/cs42");
        Assert.Equal(3, calc.Views);
        Assert.Equal(2, calc.Visitors);

        DailyReferrerStat google = Assert.Single(referrers, r => r.ReferrerHost == "www.google.com");
        Assert.Equal(2, google.Views);

        DailyEventStat completed = Assert.Single(eventStats, e => e.Name == "calculation-completed");
        Assert.Equal(2, completed.Count);
        Assert.Equal("al/cs42", completed.Target);
    }

    [Fact]
    public void ComputeDay_ZeroTraffic_StillProducesTheWatermarkRow()
    {
        (DailySiteStat site, List<DailyRouteStat> routes, List<DailyReferrerStat> referrers, List<DailyEventStat> events) =
            AnalyticsRollup.ComputeDay(Day, [], []);

        Assert.Equal(Day, site.Day);
        Assert.Equal(0, site.Views);
        Assert.Empty(routes);
        Assert.Empty(referrers);
        Assert.Empty(events);
    }

    [Fact]
    public void DayBounds_AreUtcKind()
    {
        // Regression guard from Portfolio: DateOnly.ToDateTime defaults to Kind=Unspecified,
        // which timestamptz-style providers reject.
        (DateTime start, DateTime end) = AnalyticsRollup.DayBounds(Day);

        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
        Assert.Equal(TimeSpan.FromDays(1), end - start);
    }

    [Theory]
    [InlineData("2026-08-20T00:00:00Z", "2026-08-20T00:20:00Z")] // before today's run -> today
    [InlineData("2026-08-20T00:20:00Z", "2026-08-21T00:20:00Z")] // at the run time -> tomorrow
    [InlineData("2026-08-20T13:00:00Z", "2026-08-21T00:20:00Z")] // after -> tomorrow
    public void NextRunUtc_TargetsTwentyPastMidnightUtc(string now, string expected) =>
        Assert.Equal(
            DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
            AnalyticsRollup.NextRunUtc(DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)));
}
