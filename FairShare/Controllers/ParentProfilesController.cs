using FairShare.Interfaces;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Controllers;

[Authorize]
public class ParentProfilesController(IParentProfileService service) : Controller
{
    private readonly IParentProfileService _service = service;

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
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

        return View(list);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
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

        return View(p);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Edit(Guid id, ParentProfile posted, CancellationToken ct)
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

        if (!ModelState.IsValid)
        {
            return View(posted);
        }

        existing.DisplayName = posted.DisplayName;
        existing.ApplyFrom(posted);
        existing.UpdatedUtc = DateTime.UtcNow;

        bool ok = await _service.UpdateAsync(existing, ct);

        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Concurrency conflict. Reload the page.");
            return View(posted);
        }

        TempData["Msg"] = "Saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
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
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Create(ParentProfile model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(nameId))
        {
            return Forbid();
        }
        Guid currentId = Guid.Parse(nameId);
        model.OwnerUserId = currentId;
        model.CreatedUtc = DateTime.UtcNow;

        await _service.CreateAsync(model, ct);
        return RedirectToAction(nameof(Index));
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
