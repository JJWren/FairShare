using FairShare.Interfaces;
using FairShare.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Controllers;

public class SupportController(IStateGuidelineCatalog catalog, ILogger<SupportController> logger) : Controller
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    [HttpGet]
    public IActionResult Index(string state, string form)
    {
        IChildSupportCalculator? calc = _catalog.GetCalculator(state, form);

        if (calc is null)
        {
            return NotFound();
        }

        CalculatorViewModel vm = new()
        {
            State = state.ToUpperInvariant(),
            Form = form.ToUpperInvariant(),
            IsSharedCustodyForm = calc.IsSharedCustody
        };

        ViewData["Title"] = $"{vm.State} - {vm.Form} Calculator";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(string state, string form, string? action, CalculatorViewModel vm)
    {
        vm.State = state.ToUpperInvariant();
        vm.Form = form.ToUpperInvariant();
        IChildSupportCalculator? calc = _catalog.GetCalculator(state, form);

        if (calc is null)
        {
            return NotFound();
        }

        vm.IsSharedCustodyForm = calc.IsSharedCustody;

        if (action == "reset")
        {
            ModelState.Clear();
            CalculatorViewModel clean = new()
            {
                State = vm.State,
                Form = vm.Form,
                IsSharedCustodyForm = vm.IsSharedCustodyForm
            };
            ViewData["Title"] = $"{clean.State} - {clean.Form} Calculator";
            return View(clean);
        }

        if (!vm.IsSharedCustodyForm)
        {
            if (vm.Plaintiff.HasPrimaryCustody == vm.Defendant.HasPrimaryCustody)
            {
                ModelState.AddModelError(string.Empty, "For this form, you must mark exactly one parent as having Primary Custody.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"{vm.State} - {vm.Form} Calculator";
            return View(vm);
        }

        vm.Result = calc.Calculate(vm.Plaintiff, vm.Defendant, vm.NumberOfChildren);
        vm.Submitted = true;
        ViewData["Title"] = $"{vm.State} - {vm.Form} Calculator";
        return View(vm);
    }
}
