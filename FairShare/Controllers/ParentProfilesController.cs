using FairShare.Interfaces;
using FairShare.Models;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Controllers;

public class ParentProfilesController(IParentProfileService service) : Controller
{
    private readonly IParentProfileService _service = service;

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        IReadOnlyList<ParentProfile> list = await _service.ListAsync(q, ct);
        return View(list);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        ParentProfile? p = await _service.GetAsync(id, ct);
        return p is null ? NotFound() : View(p);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ParentProfile posted, CancellationToken ct)
    {
        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(posted);
        }

        existing.DisplayName = posted.DisplayName;
        existing.ApplyFrom(posted);
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
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _service.ArchiveAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }
}
