using FairShare.Helpers;
using FairShare.Models;

namespace FairShare.Interfaces
{
    public interface IChildSupportCalculator
    {
        /// <summary>
        /// The two-letter abbreviation for the state this calculator is designed for.
        /// </summary>
        string State { get; }

        /// <summary>
        /// The specific form or guideline this calculator implements within the state.
        /// </summary>
        string Form { get; }

        /// <summary>
        /// Indicates whether this calculator implements a shared custody guideline variant.
        /// </summary>
        bool IsSharedCustody { get; }

        /// <summary>
        /// Calculates the final child support obligation for both parents and determines which parent is the payer based on the
        /// <see cref="ParentData"/> provided for each parent and the number of children.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>The paying parent as a <see cref="string"/> and the amount the parent owes as an <seealso cref="int"/>.</returns>
        CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren);
    }
}
