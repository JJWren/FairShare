using System.ComponentModel.DataAnnotations;
using FairShare.CustomObjects;
using FairShare.Interfaces;
using FairShare.ViewModels;

using Microsoft.AspNetCore.Mvc;

namespace FairShare.Areas.AL.Controllers
{
    [Area("AL")]
    public class CS42SController(IChildSupportCalculator calculator, ILogger<CS42SController> logger) : Controller
    {
        private readonly IChildSupportCalculator _calculator = calculator;
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

        private ResultsViewModel GetFinalCalculation(CS42SViewModel vm)
        {
            CalculationResult calculationResult = _calculator.Calculate(vm.Plaintiff, vm.Defendant, vm.NumberOfChildren);

            return new ()
            {
                Payer = calculationResult.Payer,
                FinalAmount = calculationResult.FinalAmount < 0 ? calculationResult.FinalAmount * -1 : calculationResult.FinalAmount,
            };
        }
    }
}
