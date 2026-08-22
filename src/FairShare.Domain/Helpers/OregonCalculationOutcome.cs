using System.Collections.Generic;
using FairShare.Domain.Models;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// The result of an Oregon worksheet calculation. Unlike Alabama's single payer-and-amount,
    /// Oregon can obligate both parents at once (Children Attending School are paid directly by
    /// each parent), so both totals are first-class here alongside the worksheet lines.
    /// </summary>
    public sealed record OregonCalculationOutcome
    {
        public bool Success { get; init; }

        public IReadOnlyList<CalcError> Errors { get; init; } = [];

        /// <summary>Every numeric line of the worksheet in form order; empty when the calculation failed.</summary>
        public IReadOnlyList<WorksheetLine> Lines { get; init; } = [];

        /// <summary>
        /// Line 7c: the parent who should pay support for the minor children, or
        /// <see langword="null"/> when neither should (equal net obligations, or no minor children).
        /// </summary>
        public ParentType? PaysForMinorChildren { get; init; }

        /// <summary>Line 9e: the plaintiff's total monthly support (cash child support + cash medical, minors + CAS).</summary>
        public decimal PlaintiffTotalSupport { get; init; }

        /// <summary>Line 9e: the defendant's total monthly support.</summary>
        public decimal DefendantTotalSupport { get; init; }

        /// <summary>Lines 4f/9f: who will provide the joint children's health care coverage.</summary>
        public CoverageProvider CoverageProvider { get; init; } = CoverageProvider.EitherWhenAvailable;

        /// <summary>Line 9g: the reasonable cost cap for health care coverage to name in the order.</summary>
        public decimal ReasonableCostTotal { get; init; }

        /// <summary>The effective date of the OAR 137-050 rule parameters this estimate implements.</summary>
        public System.DateOnly RuleEffectiveDate { get; init; }
    }
}
