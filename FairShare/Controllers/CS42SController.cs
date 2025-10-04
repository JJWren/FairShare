using FairShare.CustomObjects;
using FairShare.Managers;
using FairShare.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Controllers
{
    public class CS42SController(ILogger<CS42SController> logger) : Controller
    {
        private readonly ILogger<CS42SController> _logger = logger;

        [HttpGet]
        public IActionResult Index()
        {
            // Sample data for demonstration purposes
            CS42SViewModel vm = new ()
            {
                NumberOfChildren = 4,
                Plaintiff = new ()
                {
                    MonthlyGrossIncome = 4244,
                    PreexistingChildSupport = 0,
                    PreexistingAlimony = 0,
                    WorkRelatedChildcareCosts = 0,
                    HealthcareCoverageCosts = 0,
                },
                Defendant = new ()
                {
                    MonthlyGrossIncome = 8462,
                    PreexistingChildSupport = 0,
                    PreexistingAlimony = 1000,
                    WorkRelatedChildcareCosts = 0,
                    HealthcareCoverageCosts = 292,
                },
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(CS42SViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                vm.Results = GetFinalCalculation(vm);
                vm.Saved = true;
                return View(vm);
            }
            catch (ValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                _logger.LogWarning(ex, "Domain validation failed while calculating CS-42-S");
                return View(vm);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                ModelState.AddModelError(nameof(vm.NumberOfChildren), ex.Message);
                _logger.LogWarning(ex, "Out-of-range input");
                return View(vm);
            }
        }

        private static ResultsViewModel GetFinalCalculation(CS42SViewModel cS42SViewModel)
        {
            FinalCalcWithPayerCO finalCalcWithPayer =
                CS42SManager.GetFinalCalculation(cS42SViewModel.Plaintiff, cS42SViewModel.Defendant, cS42SViewModel.NumberOfChildren);

            return new()
            {
                Payer = finalCalcWithPayer.Payer,
                FinalAmount = (finalCalcWithPayer.FinalAmount < 0) ? finalCalcWithPayer.FinalAmount * -1 : finalCalcWithPayer.FinalAmount,
            };
        }
    }
}
