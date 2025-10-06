using FairShare.Helpers;
using FairShare.Models;
using static FairShare.Helpers.Enums;

namespace FairShare.Calculators
{
    /// <summary>
    /// The calculator for Form CS-42-S, currently implementing the shared custody guidelines as per Alabama state law.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public class CS42SCalculator(ILogger<CS42SCalculator> logger) : BaseChildSupportCalculator
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<CS42SCalculator> _logger = logger;

        /// <summary>
        /// The two-letter abbreviation for the state this calculator is designed for.
        /// </summary>
        public override string State => States.AL.ToString();

        /// <summary>
        /// The specific form or guideline this calculator implements within the state.
        /// </summary>
        public override string Form => Forms.CS42S.ToString();

        /// <summary>
        /// Indicates if the calculator is for shared custody.
        /// </summary>
        public override bool IsSharedCustody => true;

        /// <summary>
        /// Calculates the final child support obligation for both parents and determines which parent is the payer based on the
        /// <see cref="ParentData"/> provided for each parent and the number of children.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>The paying parent as a <see cref="string"/> and the amount the parent owes as an <seealso cref="int"/>.</returns>
        public override CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            CalculationResult result = CreateResultShell(numberOfChildren);

            try
            {
                if (numberOfChildren <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(numberOfChildren), "Number of children must be greater than 0.");
                }

                int combinedAdjustedGrossIncome = GetCombinedMonthlyAdjustedGrossIncome(
                    plaintiff.GetMonthlyAdjustedGrossIncome(),
                    defendant.GetMonthlyAdjustedGrossIncome());
                int totalChildSupportObligation = GetTotalChildSupportObligation(plaintiff, defendant, numberOfChildren);
                decimal plaintiffIncomePercentage = GetPercentageShareOfIncome(
                    plaintiff.GetMonthlyAdjustedGrossIncome(),
                    combinedAdjustedGrossIncome);
                int plaintiffChildSupportObligation = (int)Math.Round(totalChildSupportObligation * plaintiffIncomePercentage, 0);
                int defendantChildSupportObligation = totalChildSupportObligation - plaintiffChildSupportObligation;
                int sharedBcsoCredit = GetSharedBcsoCredit(numberOfChildren, combinedAdjustedGrossIncome);
                int plaintiffTotalCostsPaid = plaintiff.GetTotalChildcareAndHealthcareCosts();
                int defendantTotalCostsPaid = defendant.GetTotalChildcareAndHealthcareCosts();
                int plaintiffFinalCalculation = GetAdjustedSharedChildSupportObligation(
                    plaintiffChildSupportObligation,
                    plaintiffTotalCostsPaid,
                    sharedBcsoCredit);
                int defendantFinalCalculation = GetAdjustedSharedChildSupportObligation(
                    defendantChildSupportObligation,
                    defendantTotalCostsPaid,
                    sharedBcsoCredit);

                if (plaintiffFinalCalculation == defendantFinalCalculation)
                {
                    result.Success = true;
                    result.Payer = "Neither";
                    result.FinalAmount = 0;
                    return result;
                }

                if (plaintiffFinalCalculation >= defendantFinalCalculation)
                {
                    result.Success = true;
                    result.Payer = "Plaintiff";
                    result.FinalAmount = plaintiffFinalCalculation;
                }
                else
                {
                    result.Success = true;
                    result.Payer = "Defendant";
                    result.FinalAmount = defendantFinalCalculation;
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                result.Success = false;

                string code = ex.ParamName switch
                {
                    "numberOfChildren" => "INVALID_CHILD_COUNT",
                    "combinedAdjustedGrossIncome" => "CAGI_OUT_OF_RANGE",
                    _ => "ARG_OUT_OF_RANGE"
                };

                result.Errors.Add(new CalcError
                {
                    Code = code,
                    Message = ex.Message,
                    Field = ex.ParamName,
                    Severity = ErrorSeverity.Error
                });

                _logger.LogError(ex, "Calculation failed: {Code} (field: {Field})", code, ex.ParamName);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(new ()
                {
                    Code = "UNEXPECTED_ERROR",
                    Message = "An unexpected error occurred during calculation.",
                    Field = null,
                    Severity = ErrorSeverity.Error
                });
                _logger.LogError(ex, "Unexpected calculation error");
            }

            return result;
        }

        /// <summary>
        /// Calculates the shared custody basic child support obligation (150% BCSO) based on the number of children and the
        /// combined adjusted gross income of the parents.
        /// </summary>
        /// <param name="numberOfChildren">The number of children for whom support is being calculated. Must be a positive integer.</param>
        /// <param name="combinedAdjustedGrossIncome">
        /// The combined adjusted gross income of both parents, expressed in whole currency units. Must be a non-negative integer.
        /// </param>
        /// <returns>The calculated shared custody basic child support obligation (150% BCSO), rounded to the nearest whole number.</returns>
        private static int GetSharedCustodyBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
            => (int)Math.Round(GetBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome) * 1.5, 0);

        /// <summary>
        /// Gets the total child support obligation, which includes the shared custody basic child support obligation and the total childcare and
        /// healthcare costs.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>The sum of the the SPCA BCSO, total childcare costs, and total healthcare costs as an <see cref="int"/>.</returns>
        private static int GetTotalChildSupportObligation(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            int sharedBCSO = GetSharedCustodyBasicChildSupportObligation(
                numberOfChildren,
                GetCombinedMonthlyAdjustedGrossIncome(plaintiff.GetMonthlyAdjustedGrossIncome(), defendant.GetMonthlyAdjustedGrossIncome()));
            int totalChildcareAndHealthcareCosts = GetTotalChildcareAndHealthcareCosts(
                plaintiff.GetTotalChildcareAndHealthcareCosts(),
                defendant.GetTotalChildcareAndHealthcareCosts());
            return sharedBCSO + totalChildcareAndHealthcareCosts;
        }

        /// <summary>
        /// Gets the shared BCSO credit, which is 50% of the shared custody basic child support obligation (150% BCSO).
        /// </summary>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <param name="combinedAdjustedGrossIncome">
        ///     The combined adjusted gross income of both parents, in whole dollars. Must be a non-negative integer.
        /// </param>
        /// <returns>The shared BCSO credit (50% of the SPCA BCSO total) as an <see cref="int"/>.</returns>
        private static int GetSharedBcsoCredit(int numberOfChildren, int combinedAdjustedGrossIncome)
        {
            return (int)Math.Round(GetSharedCustodyBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome) * 0.5, 0);
        }

        /// <summary>
        /// Gets the adjusted shared child support obligation for a parent by subtracting the total costs paid and the shared BCSO credit from the
        /// total obligation of that parent.
        /// </summary>
        /// <param name="parentChildSupportObligation">
        ///     The child support obligation of a parent calculated by taking the parent's percentage of the combined adjusted gross income and
        ///     multiplying it by the shared BCSO.
        /// </param>
        /// <param name="parentTotalCostsPaid">The total costs paid for the children (childcare + healthcare costs).</param>
        /// <param name="sharedBcsoCredit">50% of the shared BCSO amount.</param>
        /// <returns>The adjusted shared child support obligation for the parent as an <see cref="int"/>.</returns>
        private static int GetAdjustedSharedChildSupportObligation(int parentChildSupportObligation, int parentTotalCostsPaid, int sharedBcsoCredit)
        {
            return parentChildSupportObligation - (parentTotalCostsPaid + sharedBcsoCredit);
        }
    }
}
