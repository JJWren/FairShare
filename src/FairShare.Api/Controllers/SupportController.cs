using System;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FairShare.Api.Controllers;

/// <summary>
/// First-party donate redirect (Portfolio's /go pattern): the SPA links here so the click
/// can be counted as an anonymous donate-click event before the 302 to the configured,
/// admin-controlled destination - no open redirect, payment happens entirely off-site.
/// Unconfigured instances 404 and the SPA hides the Support surface.
/// </summary>
[Route("go")]
[AllowAnonymous]
public class SupportController(IAnalyticsService analytics, IConfiguration configuration) : ControllerBase
{
    private readonly IAnalyticsService _analytics = analytics;
    private readonly IConfiguration _configuration = configuration;

    [HttpGet("donate")]
    public async Task<IActionResult> Donate(CancellationToken ct)
    {
        string? destination = _configuration["Donations:BuyMeACoffeeUrl"];

        if (string.IsNullOrWhiteSpace(destination)
            || !Uri.TryCreate(destination, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return NotFound();
        }

        // Best-effort and rule-filtered (bots, DNT/GPC, the admin) like every event;
        // the redirect happens regardless of whether anything was recorded.
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.DonateClick, target: null, ct);

        return Redirect(uri.ToString());
    }
}
