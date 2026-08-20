using System;

namespace FairShare.Api.Observability;

/// <summary>
/// One accountability record: sign-ins, account changes, verbose-mode toggles. Retained ~1 year
/// and deliberately not FK-bound to users - an audit row must outlive the account it names
/// (ADR 0003/0004), so actor identity is copied as it was at the time of the action.
/// </summary>
public class AuditEvent
{
    public const int MaxActorNameLength = 64;
    public const int MaxActionLength = 64;
    public const int MaxTargetLength = 128;
    public const int MaxDetailLength = 256;

    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Detail { get; set; }
}
