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

/// <summary>
/// Saved parent profiles. Strictly owner-scoped, mirroring scenarios: there is no admin
/// cross-access to other people's saved income figures, and owner mismatches read as
/// 404, never 403, so existence is not disclosed.
/// </summary>
[Authorize]
[Route("api/v1/parents")]
[ApiController]
public class ParentsController(IParentProfileService service, ILogger<ParentsController> logger) : ControllerBase
{
    private readonly IParentProfileService _service = service;
    private readonly ILogger<ParentsController> _logger = logger;

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

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
        // Guests see an empty list (the disabled picker advertises the account feature);
        // so does a token with no usable user id.
        if (IsGuest || CurrentUserId is not Guid uid)
        {
            return Ok(Array.Empty<ParentProfileDto>());
        }

        IReadOnlyList<ParentProfile> owned = await _service.ListAsync(uid, q, ct);
        return Ok(owned.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (IsGuest || CurrentUserId is not Guid uid)
        {
            return NotFound();
        }

        ParentProfile? p = await _service.GetAsync(id, ct);

        if (p is null || p.OwnerUserId != uid)
        {
            return NotFound();
        }

        return Ok(ToDto(p));
    }

    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Create([FromBody] ParentProfileCreateRequest request, CancellationToken ct)
    {
        // Every new profile gets an owner; the ownerless shape exists only as legacy data.
        if (CurrentUserId is not Guid uid)
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

        // Re-saving a named parent updates that record in place - the display name is the
        // natural key within one user's saved parents (DB-enforced by a unique index), so
        // same-named duplicates can't accumulate. An omitted name gets a generated one
        // (random suffix keeps it unique under that same index).
        string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? $"Parent {DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}"
            : request.DisplayName;

        (ParentProfile profile, bool created) = await _service.UpsertByNameAsync(data, displayName, uid, ct);

        return created
            ? CreatedAtAction(nameof(Get), new { id = profile.Id }, ToDto(profile))
            : Ok(ToDto(profile));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ParentProfileUpdateRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not Guid uid)
        {
            return NotFound();
        }

        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null || existing.OwnerUserId != uid)
        {
            return NotFound();
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
            return Conflict("The profile was modified by another request (reload and try again), or the new name is already used by another of your saved parents (choose a different name).");
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        if (CurrentUserId is not Guid uid)
        {
            return NotFound();
        }

        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null || existing.OwnerUserId != uid)
        {
            return NotFound();
        }

        bool ok = await _service.ArchiveAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
