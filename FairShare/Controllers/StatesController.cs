using FairShare.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FairShare.Controllers;

public class StatesController(IStateGuidelineCatalog catalog, ILogger<StatesController> logger) : Controller
{
    private readonly IStateGuidelineCatalog _catalog = catalog;
    private readonly ILogger<StatesController> _logger = logger;

    public IActionResult Index(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return RedirectToAction("Index", "Home");
        }

        IReadOnlyCollection<(string Form, string DisplayName)> forms = _catalog.GetFormsForState(state);

        if (forms.Count == 0)
        {
            return NotFound();
        }

        ViewData["State"] = state.ToUpperInvariant();
        return View(forms);
    }

    [HttpPost("States/{state}/Select")]
    [ValidateAntiForgeryToken]
    public IActionResult Select(string state, string selectedForm)
    {
        _logger.LogInformation("StatesController.Select POST hit. State={State} Form={Form}", state, selectedForm);

        if (string.IsNullOrWhiteSpace(state))
        {
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(selectedForm))
        {
            TempData["Error"] = "Please select a form.";
            return RedirectToAction(nameof(Index), new { state });
        }

        return RedirectToRoute("calculator", new { state, form = selectedForm });
    }
}
