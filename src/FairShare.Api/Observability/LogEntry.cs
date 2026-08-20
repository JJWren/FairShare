using System;

namespace FairShare.Api.Observability;

/// <summary>
/// One diagnostic log row. Retained 30 days (see <see cref="ObservabilityMaintenanceService"/>).
/// Never contains case content, names, or money amounts (ADR 0003).
/// </summary>
public class LogEntry
{
    public const int MaxCategoryLength = 160;
    public const int MaxMessageLength = 2000;
    public const int MaxExceptionLength = 4000;

    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Microsoft.Extensions.Logging.LogLevel as its integer value.</summary>
    public int Level { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
