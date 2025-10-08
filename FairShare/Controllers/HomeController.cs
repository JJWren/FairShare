using FairShare.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Controllers;

public class HomeController(IStateGuidelineCatalog catalog) : Controller
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    public IActionResult Index()
    {
        IReadOnlyCollection<string> states = _catalog.GetStates();
        return View(states);
    }
}
