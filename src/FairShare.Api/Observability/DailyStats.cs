using System;

namespace FairShare.Api.Observability;

/// <summary>
/// Permanent per-UTC-day aggregate. The maximum Day also serves as the rollup watermark:
/// zero-traffic days still get a row so the watermark always advances.
/// </summary>
public class DailySiteStat
{
    public DateOnly Day { get; set; }
    public long Views { get; set; }

    /// <summary>Distinct visitor keys that day ("Daily visitors" - never "unique visitors").</summary>
    public long Visitors { get; set; }
}

public class DailyRouteStat
{
    public long Id { get; set; }
    public DateOnly Day { get; set; }
    public string Path { get; set; } = string.Empty;
    public long Views { get; set; }
    public long Visitors { get; set; }
}

public class DailyReferrerStat
{
    public long Id { get; set; }
    public DateOnly Day { get; set; }
    public string ReferrerHost { get; set; } = string.Empty;
    public long Views { get; set; }
}

public class DailyEventStat
{
    public long Id { get; set; }
    public DateOnly Day { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Target { get; set; }
    public long Count { get; set; }
}
