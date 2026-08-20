using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Contracts.Observability;

public class PageViewRequest
{
    [Required, MaxLength(300)]
    public string Path { get; set; } = string.Empty;

    // Only sent on the SPA's first load; route changes have no meaningful referrer.
    [MaxLength(2000)]
    public string? Referrer { get; set; }
}

public class ClientEventRequest
{
    [Required, MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Target { get; set; }
}

public class StatsSummaryResponse
{
    public long PageViews { get; set; }
    public long DailyVisitors { get; set; }
    public long CalculationsCompleted { get; set; }
    public long GatedHits { get; set; }
    public long DonateClicks { get; set; }
    public DateOnly? FirstDay { get; set; }
}

public class PageStatRow
{
    public string Path { get; set; } = string.Empty;
    public long Views { get; set; }
    public long Visitors { get; set; }
}

public class ReferrerStatRow
{
    public string ReferrerHost { get; set; } = string.Empty;
    public long Views { get; set; }
}

public class EventStatRow
{
    public string Name { get; set; } = string.Empty;
    public string? Target { get; set; }
    public long Count { get; set; }
}

public class LogEntryRow
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

public class AuditEventRow
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Detail { get; set; }
}

public class VerboseStatusResponse
{
    public bool Enabled { get; set; }
    public DateTime? UntilUtc { get; set; }
}

public class VerboseRequest
{
    public bool Enabled { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
