using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Api.Persistence;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/v1/admin/logs")]
public class LogsController(FairShareDbContext db, LogLevelSwitch levelSwitch, IAuditService audit) : ControllerBase
{
    private readonly FairShareDbContext _db = db;
    private readonly LogLevelSwitch _levelSwitch = levelSwitch;
    private readonly IAuditService _audit = audit;

    /// <param name="level">Minimum level to include (name or number); null shows everything captured.</param>
    /// <param name="search">Case-insensitive substring over message and category.</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<LogEntryRow>>> GetLogs(
        string? level, string? search, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        IQueryable<LogEntry> query = _db.Logs.AsNoTracking();

        if (TryParseLevel(level, out LogLevel minimum))
        {
            int min = (int)minimum;
            query = query.Where(l => l.Level >= min);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(l => EF.Functions.Like(l.Message, term) || EF.Functions.Like(l.Category, term));
        }

        int total = await query.CountAsync(ct);

        List<LogEntryRow> items = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogEntryRow
            {
                Id = l.Id,
                OccurredAtUtc = l.OccurredAtUtc,
                Level = ((LogLevel)l.Level).ToString(),
                Category = l.Category,
                Message = l.Message,
                Exception = l.Exception
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<LogEntryRow> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditEventRow>>> GetAuditEvents(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        IQueryable<AuditEvent> query = _db.AuditEvents.AsNoTracking();
        int total = await query.CountAsync(ct);

        List<AuditEventRow> items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEventRow
            {
                Id = a.Id,
                OccurredAtUtc = a.OccurredAtUtc,
                ActorName = a.ActorName,
                Action = a.Action,
                Target = a.Target,
                Detail = a.Detail
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<AuditEventRow> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("verbose")]
    public ActionResult<VerboseStatusResponse> GetVerbose() => Ok(BuildVerboseStatus());

    [HttpPut("verbose")]
    public async Task<ActionResult<VerboseStatusResponse>> SetVerbose([FromBody] VerboseRequest request, CancellationToken ct)
    {
        if (request.Enabled)
        {
            _levelSwitch.EnableVerbose();
            await _audit.WriteAsync(AuditActions.VerboseEnabled, detail: $"auto-off {LogLevelSwitch.VerboseDuration.TotalHours:0.#} h", ct: ct);
        }
        else
        {
            _levelSwitch.DisableVerbose();
            await _audit.WriteAsync(AuditActions.VerboseDisabled, ct: ct);
        }

        return Ok(BuildVerboseStatus());
    }

    private VerboseStatusResponse BuildVerboseStatus() => new()
    {
        Enabled = _levelSwitch.VerboseEnabled,
        UntilUtc = _levelSwitch.VerboseUntilUtc
    };

    private static bool TryParseLevel(string? level, out LogLevel parsed)
    {
        parsed = LogLevel.Trace;
        return !string.IsNullOrWhiteSpace(level) && Enum.TryParse(level, ignoreCase: true, out parsed) && parsed != LogLevel.None;
    }
}
