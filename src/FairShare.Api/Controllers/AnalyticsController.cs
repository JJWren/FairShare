using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

/// <summary>
/// First-party beacon endpoints (ADR 0003). Anonymous on purpose - guests are the audience -
/// and covered by the global per-IP rate limiter. Every capture rule (opt-out, bot, admin
/// exclusion, path/name validation) is applied server-side; the response is 204 regardless,
/// so callers learn nothing about what was or wasn't recorded. Deliberately NOT
/// [ApiController]: its automatic 400 on model-binding failures would break the 204-always
/// contract, so malformed bodies bind to null and are simply not recorded.
/// </summary>
[Route("api/v1/analytics")]
[AllowAnonymous]
public class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    private readonly IAnalyticsService _analytics = analytics;

    [HttpPost("page-views")]
    public async Task<IActionResult> RecordPageView([FromBody] PageViewRequest? request, CancellationToken ct)
    {
        if (request is not null)
        {
            await _analytics.RecordPageViewAsync(HttpContext, request.Path, request.Referrer, ct);
        }

        return NoContent();
    }

    [HttpPost("events")]
    public async Task<IActionResult> RecordEvent([FromBody] ClientEventRequest? request, CancellationToken ct)
    {
        // Whitelist: browsers may only post gated-hit; server-observed events (calculations,
        // donate clicks) are recorded server-side and cannot be forged from a client.
        if (request is not null
            && AnalyticsRules.TryNormalizeClientEvent(request.Name, request.Target, out string name, out string? target))
        {
            await _analytics.RecordEventAsync(HttpContext, name, target, ct);
        }

        return NoContent();
    }
}
