using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using FairShare.Api.Services;
using FairShare.Contracts.Parents;
using FairShare.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Api.Controllers;

[Authorize]
[Route("api/v1/parents")]
[ApiController]
public class ParentsController(IParentProfileService service, ILogger<ParentsController> logger) : ControllerBase
{
    private readonly IParentProfileService _service = service;
    private readonly ILogger<ParentsController> _logger = logger;

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    private bool IsAdmin => User.IsInRole("Admin");
    private bool IsGuest => User.HasClaim(c => c.Type == "guest" && c.Value == "true");

    private static ParentProfileDto ToDto(ParentProfile p) => new()
    {
        Id = p.Id,
        DisplayName = p.DisplayName,
        MonthlyGrossIncome = p.MonthlyGrossIncome,
        PreexistingChildSupport = p.PreexistingChildSupport,
        PreexistingAlimony = p.PreexistingAlimony,
        WorkRelatedChildcareCosts = p.WorkRelatedChildcareCosts,
        HealthcareCoverageCosts = p.HealthcareCoverageCosts,
        HasPrimaryCustody = p.HasPrimaryCustody,
        CreatedUtc = p.CreatedUtc,
        UpdatedUtc = p.UpdatedUtc,
        RowVersion = p.RowVersion is null ? null : Convert.ToBase64String(p.RowVersion)
    };

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
    {
        IReadOnlyList<ParentProfile> all = await _service.ListAsync(q, ct);

        if (!IsAdmin)
        {
            Guid? uid = CurrentUserId;
            if (uid is null || IsGuest)
            {
                return Ok(Array.Empty<ParentProfileDto>());
            }

            all = all.Where(p => p.OwnerUserId == uid).ToList();
        }

        return Ok(all.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        ParentProfile? p = await _service.GetAsync(id, ct);

        if (p is null)
        {
            return NotFound();
        }

        if (!IsAdmin)
        {
            if (IsGuest) return NotFound();
            Guid? uid = CurrentUserId;
            if (p.OwnerUserId != uid) return NotFound();
        }

        return Ok(ToDto(p));
    }

    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Create([FromBody] ParentProfileCreateRequest request, CancellationToken ct)
    {
        Guid? uid = CurrentUserId;

        if (!IsAdmin && uid is null)
        {
            return Forbid();
        }

        ParentData data = new()
        {
            MonthlyGrossIncome = request.MonthlyGrossIncome,
            PreexistingChildSupport = request.PreexistingChildSupport,
            PreexistingAlimony = request.PreexistingAlimony,
            WorkRelatedChildcareCosts = request.WorkRelatedChildcareCosts,
            HealthcareCoverageCosts = request.HealthcareCoverageCosts,
            HasPrimaryCustody = request.HasPrimaryCustody
        };

        ParentProfile profile;

        if (request.Deduplicate)
        {
            profile = await _service.GetOrCreateAsync(data, request.DisplayName, uid, ct);
        }
        else
        {
            profile = await _service.CreateAsync(new ParentProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? $"Parent {DateTime.UtcNow:yyyyMMdd-HHmmss}"
                    : request.DisplayName.Trim(),
                MonthlyGrossIncome = data.MonthlyGrossIncome,
                PreexistingChildSupport = data.PreexistingChildSupport,
                PreexistingAlimony = data.PreexistingAlimony,
                WorkRelatedChildcareCosts = data.WorkRelatedChildcareCosts,
                HealthcareCoverageCosts = data.HealthcareCoverageCosts,
                HasPrimaryCustody = data.HasPrimaryCustody,
                CreatedUtc = DateTime.UtcNow,
                OwnerUserId = uid
            }, ct);
        }

        return CreatedAtAction(nameof(Get), new { id = profile.Id }, ToDto(profile));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ParentProfileUpdateRequest request, CancellationToken ct)
    {
        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        if (!IsAdmin)
        {
            Guid? uid = CurrentUserId;

            if (IsGuest || existing.OwnerUserId != uid)
            {
                return NotFound();
            }
        }

        byte[]? expectedRowVersion = null;

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                expectedRowVersion = Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException)
            {
                return BadRequest("RowVersion must be the base64 value returned by GET.");
            }
        }

        existing.DisplayName = request.DisplayName.Trim();
        existing.MonthlyGrossIncome = request.MonthlyGrossIncome;
        existing.PreexistingChildSupport = request.PreexistingChildSupport;
        existing.PreexistingAlimony = request.PreexistingAlimony;
        existing.WorkRelatedChildcareCosts = request.WorkRelatedChildcareCosts;
        existing.HealthcareCoverageCosts = request.HealthcareCoverageCosts;
        existing.HasPrimaryCustody = request.HasPrimaryCustody;
        existing.UpdatedUtc = DateTime.UtcNow;

        bool ok = await _service.UpdateAsync(existing, expectedRowVersion, ct);

        if (!ok)
        {
            return Conflict("The profile was modified by another request. Reload and try again.");
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        if (!IsAdmin)
        {
            Guid? uid = CurrentUserId;

            if (IsGuest || existing.OwnerUserId != uid)
            {
                return NotFound();
            }
        }

        bool ok = await _service.ArchiveAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
