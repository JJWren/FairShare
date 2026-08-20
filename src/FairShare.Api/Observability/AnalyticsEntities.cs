using System;

namespace FairShare.Api.Observability;

/// <summary>
/// One qualifying page view. Raw rows live 90 days, then only the Daily* aggregates remain.
/// VisitorKey is an HMAC whose payload includes the UTC date, so keys cannot be linked
/// across days; the IP and user-agent that feed it are never stored (ADR 0003).
/// </summary>
public class PageView
{
    public const int MaxPathLength = 300;
    public const int MaxReferrerHostLength = 200;
    public const int MaxVisitorKeyLength = 64;

    public long Id { get; set; }
    public string Path { get; set; } = string.Empty;

    /// <summary>Host only, external referrers only; null for direct or internal navigation.</summary>
    public string? ReferrerHost { get; set; }

    public string VisitorKey { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>
/// One content-free analytics event: a name plus a coarse target (form key, gate name),
/// never case content (NFR-1). Raw rows live 90 days.
/// </summary>
public class AnalyticsEvent
{
    public const int MaxNameLength = 40;
    public const int MaxTargetLength = 300;

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string VisitorKey { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>Singleton row (Id = 1) holding the per-install HMAC secret for visitor keys.</summary>
public class AnalyticsState
{
    public int Id { get; set; }
    public string SecretBase64 { get; set; } = string.Empty;
}
