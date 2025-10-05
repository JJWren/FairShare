namespace FairShare.Controllers
{
    using FairShare.Interfaces;
    using FairShare.ViewModels;
    using Microsoft.AspNetCore.Mvc;

    public class HomeController(ICalculatorRegistry registry) : Controller
    {
        private readonly ICalculatorRegistry _registry = registry;

        [HttpGet]
        public IActionResult Index() =>
            View(new HomeViewModel
            {
                States = _registry.List()
                    .GroupBy(x => x.State)
                    .Select(g => g.Key)
                    .OrderBy(s => s)
                    .ToList()
            });

        [HttpGet]
        public IActionResult Forms(string state) =>
            Json(_registry.List()
                .Where(x => x.State == state)
                .Select(x => x.Form)
                .Distinct());

        [HttpPost]
        public IActionResult Go(string state, string form)
            => RedirectToAction("Index", form, new { area = state });
    }
}
