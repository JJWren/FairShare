using FairShare.Helpers;
using FairShare.Models;

namespace FairShare.ViewModels
{
    public class CalculatorViewModel
    {
        public string State { get; set; } = string.Empty;
        public string Form { get; set; } = string.Empty;
        public int NumberOfChildren { get; set; }
        public ParentData Plaintiff { get; set; } = new();
        public ParentData Defendant { get; set; } = new();
        public CalculationResult? Result { get; set; }
        public bool Submitted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the form being used is a shared custody form.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Display(AutoGenerateField = false)]
        public bool IsSharedCustodyForm { get; set; }
    }
}
