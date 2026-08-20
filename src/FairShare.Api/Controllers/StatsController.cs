using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/v1/admin/stats")]
public class StatsController(IAnalyticsService analytics) : ControllerBase
{
    private readonly IAnalyticsService _analytics = analytics;

    /// <param name="days">Period in days ending today; null or 0 means all time.</param>
    [HttpGet("summary")]
    public async Task<ActionResult<StatsSummaryResponse>> GetSummary(int? days, CancellationToken ct) =>
        Ok(await _analytics.GetSummaryAsync(Normalize(days), ct));

    [HttpGet("pages")]
    public async Task<ActionResult<PagedResult<PageStatRow>>> GetPages(
        int? days, int page = 1, int pageSize = 10, string sort = "views", bool desc = true, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;
        return Ok(await _analytics.GetTopPagesAsync(Normalize(days), page, pageSize, sort, desc, ct));
    }

    [HttpGet("referrers")]
    public async Task<ActionResult<List<ReferrerStatRow>>> GetReferrers(int? days, CancellationToken ct) =>
        Ok(await _analytics.GetTopReferrersAsync(Normalize(days), take: 10, ct));

    [HttpGet("events")]
    public async Task<ActionResult<List<EventStatRow>>> GetEvents(int? days, CancellationToken ct) =>
        Ok(await _analytics.GetEventsAsync(Normalize(days), ct));

    private static int? Normalize(int? days) => days is int d && d > 0 ? d : null;
}
