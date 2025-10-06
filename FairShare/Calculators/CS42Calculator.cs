using FairShare.Helpers;
using FairShare.Models;
using static FairShare.Helpers.Enums;

namespace FairShare.Calculators
{
    /// <summary>
    /// The calculator for Form CS-42, currently implementing the standard custody guidelines as per Alabama state law.
    /// </summary>
    public class CS42Calculator(ILogger<CS42Calculator> logger) : BaseChildSupportCalculator
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<CS42Calculator> _logger = logger;

        /// <summary>
        /// The two-letter abbreviation for the state this calculator is designed for.
        /// </summary>
        public override string State => States.AL.ToString();

        /// <summary>
        /// The specific form or guideline this calculator implements within the state.
        /// </summary>
        public override string Form => Forms.CS42.ToString();

        /// <summary>
        /// The shared custody flag; this calculator does not implement shared custody rules.
        /// </summary>
        public override bool IsSharedCustody => false;

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
                decimal defendantIncomePercentage = GetPercentageShareOfIncome(
                    defendant.GetMonthlyAdjustedGrossIncome(),
                    combinedAdjustedGrossIncome);

                int plaintiffChildSupportObligation = (int)Math.Round(totalChildSupportObligation * plaintiffIncomePercentage, 0);
                int defendantChildSupportObligation = (int)Math.Round(totalChildSupportObligation * defendantIncomePercentage, 0);

                int plaintiffTotalCostsPaid = plaintiff.GetTotalChildcareAndHealthcareCosts();
                int defendantTotalCostsPaid = defendant.GetTotalChildcareAndHealthcareCosts();

                int plaintiffRecommendedObligation = GetParentsRecommendedChildSupportObligation(
                    plaintiffChildSupportObligation,
                    plaintiffTotalCostsPaid);
                int defendantRecommendedObligation = GetParentsRecommendedChildSupportObligation(
                    defendantChildSupportObligation,
                    defendantTotalCostsPaid);

                int plaintiffIncomeAvailableForChildSupport = GetParentsIncomeAvailableForChildSupport(plaintiff.MonthlyGrossIncome);
                int defendantIncomeAvailableForChildSupport = GetParentsIncomeAvailableForChildSupport(defendant.MonthlyGrossIncome);
                int plaintiffMaxObligationAfterSSR = GetMaxRecommendedChildSupportAmountAfterSSR(plaintiffIncomeAvailableForChildSupport);
                int defendantMaxObligationAfterSSR = GetMaxRecommendedChildSupportAmountAfterSSR(defendantIncomeAvailableForChildSupport);

                if (plaintiff.HasPrimaryCustody)
                {
                    result.Payer = Enums.ParentType.Defendant.ToString();
                    result.FinalAmount = GetFinalChildSupportAmount(defendantRecommendedObligation, defendantMaxObligationAfterSSR);
                }
                else
                {
                    result.Payer = Enums.ParentType.Plaintiff.ToString();
                    result.FinalAmount = GetFinalChildSupportAmount(plaintiffRecommendedObligation, plaintiffMaxObligationAfterSSR);
                }

                // Mark success so the view displays the result
                result.Success = true;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                result.Success = false;
                result.Errors.Add(new CalcError
                {
                    Code = "INVALID_CHILD_COUNT",
                    Message = ex.Message,
                    Field = ex.ParamName,
                    Severity = Enums.ErrorSeverity.Error
                });
                _logger.LogWarning(ex, "Validation error in {Form} calculation.", Form);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(new CalcError
                {
                    Code = "UNEXPECTED_ERROR",
                    Message = "An unexpected error occurred during calculation.",
                    Field = null,
                    Severity = Enums.ErrorSeverity.Error
                });
                _logger.LogError(ex, "Unexpected error in {Form} calculation.", Form);
            }

            return result;
        }

        /// <summary>
        /// Gets the total child support obligation, which includes the basic child support obligation and the total childcare and healthcare costs.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>The sum of the the BCSO, total childcare costs, and total healthcare costs as an <see cref="int"/>.</returns>
        private static int GetTotalChildSupportObligation(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            int combinedAdjustedGrossIncome = GetCombinedMonthlyAdjustedGrossIncome(
                plaintiff.GetMonthlyAdjustedGrossIncome(),
                defendant.GetMonthlyAdjustedGrossIncome());

            int totalChildcareAndHealthcareCosts = GetTotalChildcareAndHealthcareCosts(
                plaintiff.GetTotalChildcareAndHealthcareCosts(),
                defendant.GetTotalChildcareAndHealthcareCosts());

            int bcso = GetBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome);
            return bcso + totalChildcareAndHealthcareCosts;
        }

        /// <summary>
        /// Gets the recommended child support obligation for the given parent based on their obligation and total costs paid.
        /// </summary>
        /// <param name="parentChildSupportObligation">The child support obligation based on the percentage owed by the parent.</param>
        /// <param name="parentTotalCostsPaid">The costs paid for the child by the parent.</param>
        /// <returns>The recommended child support obligation for a parent as an <see cref="int"/>.</returns>
        private static int GetParentsRecommendedChildSupportObligation(int parentChildSupportObligation, int parentTotalCostsPaid)
        {
            int diff = parentChildSupportObligation - parentTotalCostsPaid;
            return diff <= 0 ? 0 : diff;
        }

        /// <summary>
        /// Gets the parent's income available for child support after self-support reserve (SSR) is deducted.
        /// </summary>
        /// <param name="parentMonthlyGrossIncome">The gross monthly income of a parent.</param>
        /// <returns>The parent's income available as an <see cref="int"/> for child support after SSR is deducted.</returns>
        private static int GetParentsIncomeAvailableForChildSupport(int parentMonthlyGrossIncome)
            => parentMonthlyGrossIncome - 981;

        /// <summary>
        /// Gets the parent's maximum recommended child support amount (85%) after self-support reserve (SSR) is deducted.
        /// </summary>
        /// <param name="parentIncomeAvailableForChildSupport">The parent's income available for child support after SSR is deducted.</param>
        /// <returns>
        /// The parent's maximum recommended child support amount (85%) as an <see cref="int"/> after self-support reserve SSR is deducted.
        /// </returns>
        private static int GetMaxRecommendedChildSupportAmountAfterSSR(int parentIncomeAvailableForChildSupport)
        {
            int maxAmount = (int)Math.Round(parentIncomeAvailableForChildSupport * 0.85, 0);
            return maxAmount <= 0 ? 0 : maxAmount;
        }

        /// <summary>
        /// Gets the final child support amount owed. Calculated by taking the lesser amount of the recommended vs. max obligation amounts.
        /// </summary>
        /// <param name="recommendedObligation">The parent's income available for child support after SSR is deducted.</param>
        /// <param name="maxObligationAfterSSR">
        /// The parent's maximum recommended child support amount (85%) after self-support reserve SSR is deducted.
        /// </param>
        /// <returns>
        /// The lesser amount between <paramref name="recommendedObligation"/> and <paramref name="maxObligationAfterSSR"/> as an <see cref="int"/>.
        /// </returns>
        private static int GetFinalChildSupportAmount(int recommendedObligation, int maxObligationAfterSSR)
            => recommendedObligation < maxObligationAfterSSR ? recommendedObligation : maxObligationAfterSSR;
    }
}
