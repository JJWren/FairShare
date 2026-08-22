using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;

namespace FairShare.Domain.Interfaces
{
    /// <summary>
    /// A form whose inputs fit the classic two-parents-plus-child-count shape (both Alabama
    /// worksheets). Forms with richer inputs (Oregon) implement only <see cref="IWorksheetForm"/>.
    /// </summary>
    public interface IChildSupportCalculator : IWorksheetForm
    {
        /// <summary>
        /// Calculates the final child support obligation for both parents and determines which parent is the payer based on the
        /// <see cref="ParentData"/> provided for each parent and the number of children.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>
        /// The paying parent, the amount the parent owes, and every worksheet line that produced them.
        /// Input problems are reported through <see cref="CalculationResult.Errors"/>, never thrown.
        /// </returns>
        CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren);
    }
}
