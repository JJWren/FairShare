using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Observability;

/// <summary>Well-known audit action names. Values are what the admin log viewer shows.</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "login-succeeded";
    public const string LoginFailed = "login-failed";
    public const string Registered = "registered";
    public const string PasswordChanged = "password-changed";
    public const string UserCreated = "user-created";
    public const string UserUpdated = "user-updated";
    public const string UserDeleted = "user-deleted";
    public const string PasswordReset = "password-reset";
    public const string VerboseEnabled = "verbose-enabled";
    public const string VerboseDisabled = "verbose-disabled";
}

public interface IAuditService
{
    /// <summary>
    /// Records an accountability event. Best-effort: an audit failure is logged and swallowed,
    /// never surfaced into the request that triggered it.
    /// </summary>
    Task WriteAsync(string action, string? target = null, string? detail = null, CancellationToken ct = default);
}

public class AuditService(FairShareDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger) : IAuditService
{
    private readonly FairShareDbContext _db = db;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<AuditService> _logger = logger;

    public async Task WriteAsync(string action, string? target = null, string? detail = null, CancellationToken ct = default)
    {
        try
        {
            ClaimsPrincipal? actor = _httpContextAccessor.HttpContext?.User;
            Guid? actorId = Guid.TryParse(actor?.FindFirstValue(ClaimTypes.NameIdentifier), out Guid parsed) ? parsed : null;
            string? actorName = actor?.Identity?.Name
                ?? (actor?.HasClaim(c => c.Type == "guest" && c.Value == "true") == true ? "guest" : null);

            _db.AuditEvents.Add(new AuditEvent
            {
                OccurredAtUtc = DateTime.UtcNow,
                ActorUserId = actorId,
                ActorName = Truncate(actorName, AuditEvent.MaxActorNameLength),
                Action = Truncate(action, AuditEvent.MaxActionLength)!,
                Target = Truncate(target, AuditEvent.MaxTargetLength),
                Detail = Truncate(detail, AuditEvent.MaxDetailLength)
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit write failed for action {Action}.", action);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
