using System.ComponentModel.DataAnnotations;

namespace FairShare.Models
{
    /// <summary>
    /// Represents financial data for a parent, including income, expenses, and costs related to childcare and healthcare.
    /// </summary>
    public class ParentData
    {
        /// <summary>
        /// Gets or sets a value indicating whether the parent has primary custody of the children.
        /// </summary>
        [Display(Name = "Primary Custody")]
        public bool HasPrimaryCustody { get; set; } = false;

        /// <summary>
        /// Gets or sets the monthly gross income.
        /// </summary>
        [Required, Range(0, int.MaxValue)]
        public int MonthlyGrossIncome { get; set; }

        /// <summary>
        /// Gets or sets the amount of preexisting child support obligations.
        /// </summary>
        /// <remarks>
        /// This property is used to account for child support obligations that were established prior to the current calculation.
        /// </remarks>
        [Required, Range(0, int.MaxValue)]
        public int PreexistingChildSupport { get; set; }

        /// <summary>
        /// Gets or sets the amount of preexisting alimony payments.
        /// </summary>
        /// <remarks>
        /// This property is used to account for alimony obligations that were established prior to the current calculation.
        /// </remarks>
        [Required, Range(0, int.MaxValue)]
        public int PreexistingAlimony { get; set; }

        /// <summary>
        /// Gets or sets the total cost of childcare expenses incurred for work-related purposes.
        /// </summary>
        [Required, Range(0, int.MaxValue)]
        public int WorkRelatedChildcareCosts { get; set; }

        /// <summary>
        /// Gets or sets the total cost of healthcare coverage for the children.
        /// </summary>
        [Required, Range(0, int.MaxValue)]
        public int HealthcareCoverageCosts { get; set; }

        /// <summary>
        /// Calculates the monthly adjusted gross income by subtracting preexisting child support and alimony payments from the monthly gross income.
        /// </summary>
        /// <returns>
        /// The monthly adjusted gross income as a <see cref="int"/> value. This value represents  the remaining
        /// income after deducting preexisting child support and alimony payments.
        /// </returns>
        public int GetMonthlyAdjustedGrossIncome()
        {
            return MonthlyGrossIncome - (PreexistingChildSupport + PreexistingAlimony);
        }

        /// <summary>
        /// Calculates the total cost of work-related childcare and healthcare coverage.
        /// </summary>
        /// <returns>
        /// The sum of <see cref="WorkRelatedChildcareCosts"/> and <see cref="HealthcareCoverageCosts"/> as a <see cref="decimal"/>
        /// representing the total combined cost.
        /// </returns>
        public int GetTotalChildcareAndHealthcareCosts()
        {
            return WorkRelatedChildcareCosts + HealthcareCoverageCosts;
        }
    }
}
