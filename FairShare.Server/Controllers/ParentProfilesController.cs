using FairShare.Shared.Interfaces;
using FairShare.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Server.Controllers;

/// <summary>
/// Manages parent financial profiles, allowing for retrieval, creation, modification, and archiving of profile data.
/// </summary>
/// <param name="service">The service for profile persistence operations.</param>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ParentProfilesController(IParentProfileService service) : ControllerBase
{
    private readonly IParentProfileService _service = service;

    /// <summary>
    /// Retrieves a list of parent profiles. Non-admin users are restricted to profiles they own.
    /// </summary>
    /// <param name="q">Optional search query for display name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="ParentProfile"/>.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentProfile>>> GetProfiles(string? q, CancellationToken ct)
    {
        IReadOnlyList<ParentProfile> list = await _service.ListAsync(q, ct);

        if (!User.IsInRole("Admin"))
        {
            string? nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(nameId))
            {
                return Forbid();
            }

            Guid currentId = Guid.Parse(nameId);
            list = list.Where(p => p.OwnerUserId == currentId).ToList();
        }

        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific parent profile by ID, ensuring ownership or admin status.
    /// </summary>
    /// <param name="id">The unique identifier of the profile.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The requested <see cref="ParentProfile"/> or a forbidden/not-found result.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<ParentProfile>> GetProfile(Guid id, CancellationToken ct)
    {
        ParentProfile? p = await _service.GetAsync(id, ct);

        if (p is null)
        {
            return NotFound();
        }

        if (!IsOwnerOrAdmin(p))
        {
            return Forbid();
        }

        return Ok(p);
    }

    /// <summary>
    /// Updates an existing parent profile. Restricted to the profile owner or an administrator.
    /// </summary>
    /// <param name="id">The unique identifier of the profile to update.</param>
    /// <param name="posted">The updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A no-content result on success.</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> PutProfile(Guid id, ParentProfile posted, CancellationToken ct)
    {
        if (id != posted.Id)
        {
            return BadRequest();
        }

        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        if (!IsOwnerOrAdmin(existing))
        {
            return Forbid();
        }

        existing.DisplayName = posted.DisplayName;
        existing.ApplyFrom(posted);
        existing.UpdatedUtc = DateTime.UtcNow;

        bool ok = await _service.UpdateAsync(existing, ct);

        if (!ok)
        {
            return Conflict("Concurrency conflict. Reload the data.");
        }

        return NoContent();
    }

    /// <summary>
    /// Archives a parent profile by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the profile to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A no-content result on success.</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct)
    {
        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        if (!IsOwnerOrAdmin(existing))
        {
            return Forbid();
        }

        await _service.ArchiveAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Creates a new parent profile and assigns it to the current user.
    /// </summary>
    /// <param name="model">The profile data to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="ParentProfile"/>.</returns>
    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<ActionResult<ParentProfile>> PostProfile(ParentProfile model, CancellationToken ct)
    {
        string? nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(nameId))
        {
            return Forbid();
        }
        Guid currentId = Guid.Parse(nameId);
        model.OwnerUserId = currentId;
        model.CreatedUtc = DateTime.UtcNow;

        var created = await _service.CreateAsync(model, ct);
        return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
    }

    private bool IsOwnerOrAdmin(ParentProfile p)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        string? nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(nameId))
        {
            return false;
        }

        Guid currentId = Guid.Parse(nameId);
        return p.OwnerUserId.HasValue && p.OwnerUserId.Value == currentId;
    }
}
