using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace FairShare.Api.Observability;

/// <summary>Event names the analytics pipeline records (name + coarse target, never content).</summary>
public static class AnalyticsEventNames
{
    public const string CalculationStarted = "calculation-started";
    public const string CalculationCompleted = "calculation-completed";
    public const string GatedHit = "gated-hit";
    public const string DonateClick = "donate-click";

    /// <summary>
    /// The only names a browser may post directly. Server-observed events (calculations,
    /// donate redirects) are recorded server-side so clients cannot forge them.
    /// </summary>
    public static readonly string[] ClientPostable = [GatedHit];
}

/// <summary>
/// The pure capture rules of ADR 0003. Everything here is static and unit-tested; the
/// service layer applies them, it does not interpret them.
/// </summary>
public static class AnalyticsRules
{
    // Substring markers; matching is case-insensitive. An empty/missing UA is treated as a
    // bot too - every real browser sends one.
    private static readonly string[] BotMarkers =
    [
        "bot", "crawl", "spider", "slurp", "curl", "wget", "python", "httpclient",
        "headless", "lighthouse", "preview", "facebookexternalhit", "monitor"
    ];

    // Path prefixes that are never countable: admin/auth surfaces, machine endpoints,
    // and framework internals.
    private static readonly string[] ExcludedPrefixes =
    [
        "/admin", "/login", "/register", "/account", "/go", "/healthz", "/swagger", "/api"
    ];

    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        return BotMarkers.Any(marker => userAgent.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>DNT and Global Privacy Control both mean: record nothing at all.</summary>
    public static bool OptedOut(IHeaderDictionary headers) =>
        headers["DNT"] == "1" || headers["Sec-GPC"] == "1";

    /// <summary>
    /// Normalizes an SPA route to its countable form (lowercase, no query/fragment, leading
    /// slash) or rejects it. File-looking last segments (a dot) are never countable.
    /// </summary>
    public static bool TryNormalizePath(string? rawPath, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath) || !rawPath.StartsWith('/') || rawPath.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        string path = rawPath;
        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (path.Length == 0 || path.Length > PageView.MaxPathLength)
        {
            return false;
        }

        path = path.ToLowerInvariant();

        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        if (ExcludedPrefixes.Any(prefix => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal))
            || path.StartsWith("/_", StringComparison.Ordinal))
        {
            return false;
        }

        string lastSegment = path[(path.LastIndexOf('/') + 1)..];
        if (lastSegment.Contains('.'))
        {
            return false;
        }

        normalized = path;
        return true;
    }

    /// <summary>External referrer host, or null for direct/internal/unparseable referrers.</summary>
    public static string? NormalizeReferrer(string? referrer, string? ownHost)
    {
        if (string.IsNullOrWhiteSpace(referrer) || !Uri.TryCreate(referrer, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string host = uri.Host.ToLowerInvariant();

        if (host.Length == 0 || host.Length > PageView.MaxReferrerHostLength)
        {
            return null;
        }

        return string.Equals(host, ownHost, StringComparison.OrdinalIgnoreCase) ? null : host;
    }

    /// <summary>
    /// Validates a client-posted event. Only whitelisted names pass, and targets are held to
    /// kebab-case tokens - a shape that cannot smuggle case content (NFR-1).
    /// </summary>
    public static bool TryNormalizeClientEvent(string? name, string? rawTarget, out string normalizedName, out string? normalizedTarget)
    {
        normalizedName = string.Empty;
        normalizedTarget = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string candidate = name.Trim().ToLowerInvariant();
        if (!AnalyticsEventNames.ClientPostable.Contains(candidate))
        {
            return false;
        }

        normalizedName = candidate;

        if (!string.IsNullOrWhiteSpace(rawTarget))
        {
            string target = rawTarget.Trim().ToLowerInvariant();
            if (target.Length > 40 || !target.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '/'))
            {
                return false;
            }

            normalizedTarget = target;
        }

        return true;
    }
}
