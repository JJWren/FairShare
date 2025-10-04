namespace FairShare.ViewModels
{
    using FairShare.Models;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Represents the view model for a child support calculation, including input data for both parents, the number of
    /// children involved, and the calculated results.
    /// </summary>
    public class CS42SViewModel
    {
        /// <summary>
        /// Gets or sets a value for the number of children involved in the child support calculation.
        /// </summary>
        [Required, Range(1, 6)]
        [Display(Name = "Number of Children")]
        public int NumberOfChildren {  get; set; } = 1;

        /// <summary>
        /// Gets or sets a value for the plaintiff's financial data.
        /// </summary>
        [Required]
        [Display(Name = "Plaintiff")]
        public ParentData Plaintiff {  get; set; } = new ();

        /// <summary>
        /// Gets or sets a value for the defendant's financial data.
        /// </summary>
        [Required]
        [Display(Name = "Defendant")]
        public ParentData Defendant { get; set; } = new ();

        /// <summary>
        /// Gets or sets a value for client-side scroll logic.
        /// </summary>
        public bool Saved { get; set; }

        /// <summary>
        /// Gets or sets a value for the results of the child support calculation.
        /// </summary>
        public ResultsViewModel Results { get; set; } = new ();
    }
}
