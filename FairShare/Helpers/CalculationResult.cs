using FairShare.Helpers;

namespace FairShare.CustomObjects
{
    /// <summary>
    /// Provides the result of a child support calculation, including which parent is the payer and the final amount owed.
    /// </summary>
    /// <param name="payer">The parent that has to pay the <paramref name="finalAmount"/>.</param>
    /// <param name="finalAmount">The amount owed by the <paramref name="payer"/>.</param>
    public class CalculationResult(string payer, int finalAmount)
    {
        /// <summary>
        /// Gets or sets a value indicating whether the calculation was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Collection of human-readable error messages encountered during the calculation process. 
        /// </summary>
        public List<CalcError> Errors { get; set; } = [];

        /// <summary>
        /// Gets or sets the two-letter abbreviation for the state this calculator is designed for.
        /// </summary>
        public string State { get; set; } = "AL";

        /// <summary>
        /// Gets or sets the specific form or guideline this calculator implements within the state.
        /// </summary>
        public string Form { get; set; } = "CS42S";

        /// <summary>
        /// Gets or sets the number of children shared between both parents in the child support order.
        /// </summary>
        public int NumberOfChildren { get; set; } = 1;

        /// <summary>
        /// Gets or sets the paying parent as a <see cref="string"/>.
        /// </summary>
        public string Payer { get; set; } = payer;

        /// <summary>
        /// Gets or sets the amount the paying parent owes as an <see cref="int"/>.
        /// </summary>
        public int FinalAmount { get; set; } = finalAmount;
    }
}
