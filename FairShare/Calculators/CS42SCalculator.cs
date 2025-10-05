using FairShare.CustomObjects;
using FairShare.Helpers;
using FairShare.Interfaces;
using FairShare.Models;
using FairShare.Seeds;

using static FairShare.Helpers.Enums;

namespace FairShare.Calculators
{
    /// <summary>
    /// The calculator for Form CS-42-S, currently implementing the shared custody guidelines as per Alabama state law.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public class CS42SCalculator(ILogger<CS42SCalculator> logger) : IChildSupportCalculator
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<CS42SCalculator> _logger = logger;

        /// <summary>
        /// The two-letter abbreviation for the state this calculator is designed for.
        /// </summary>
        public string State => "AL";

        /// <summary>
        /// The specific form or guideline this calculator implements within the state.
        /// </summary>
        public string Form => "CS42S";

        /// <summary>
        /// Calculates the final child support obligation for both parents and determines which parent is the payer based on the
        /// <see cref="ParentData"/> provided for each parent and the number of children.
        /// </summary>
        /// <param name="plaintiff">The plaintiff parent on the original court order.</param>
        /// <param name="defendant">The defendant parent on the original court order.</param>
        /// <param name="numberOfChildren">The number of children shared between both parents in the child support order.</param>
        /// <returns>The paying parent as a <see cref="string"/> and the amount the parent owes as an <seealso cref="int"/>.</returns>
        public CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            CalculationResult result = new ("N/A", 0)
            {
                Success = false,
                State = State,
                Form = Form,
                NumberOfChildren = numberOfChildren
            };

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
        /// Gets the combined monthly adjusted gross income of both parents by summing their individual adjusted gross incomes.
        /// </summary>
        /// <param name="plaintiffAdjustedGrossIncome">The plaintiff parent's monthly adjusted gross income.</param>
        /// <param name="defendantAdjustedGrossIncome">The defendant parent's monthly adjusted gross income.</param>
        /// <returns>The sum of the two parent's monthly gross incomes as an <see cref="int"/>.</returns>
        private static int GetCombinedMonthlyAdjustedGrossIncome(int plaintiffAdjustedGrossIncome, int defendantAdjustedGrossIncome)
        {
            return plaintiffAdjustedGrossIncome + defendantAdjustedGrossIncome;
        }

        /// <summary>
        /// Gets the percentage share of income for a parent based on their adjusted gross income relative to the combined adjusted gross income (CAGI).
        /// </summary>
        /// <param name="parentAdjustedGrossIncome">The parent's monthly adjusted gross income.</param>
        /// <param name="combinedAdjustedGrossIncome">The combined monthly adjusted gross income of both parents.</param>
        /// <returns>The percentage share of the CAGI for the input parent as a <see cref="decimal"/> rounded to two places.</returns>
        private static decimal GetPercentageShareOfIncome(int parentAdjustedGrossIncome, int combinedAdjustedGrossIncome)
        {
            if (combinedAdjustedGrossIncome == 0)
            {
                return 0;
            }

            return Math.Round((decimal)parentAdjustedGrossIncome / combinedAdjustedGrossIncome, 2);
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
        {
            return (int)Math.Round(GetBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome) * 1.5, 0);
        }

        /// <summary>
        /// Calculates the basic child support obligation based on the number of children and the combined adjusted
        /// gross income of the parents.
        /// </summary>
        /// <remarks>This method uses a lookup table to determine the obligation amount based on the 
        /// provided inputs. Ensure that the inputs fall within the valid range supported by the lookup.</remarks>
        /// <param name="numberOfChildren">The number of children for whom support is being calculated. Must be a positive integer.</param>
        /// <param name="combinedAdjustedGrossIncome">
        ///     The combined adjusted gross income of both parents, in whole dollars. Must be a non-negative integer.
        /// </param>
        /// <returns>The basic child support obligation, as an integer value in whole dollars.</returns>
        private static int GetBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
        {
            return BcsoLookup.Get(combinedAdjustedGrossIncome, numberOfChildren);
        }

        /// <summary>
        /// Sums the total childcare and healthcare costs paid by both parents.
        /// </summary>
        /// <param name="plaintiffChildcareAndHealthcareCosts">Sum of the plaintiff parent's childcare and healthcare costs.</param>
        /// <param name="defendantChildcareAndHealthcareCosts">Sum of the defendant parent's childcare and healthcare costs.</param>
        /// <returns>The sums of the plaintiff's and defendant's childcare and healthcare costs for the children as an <see cref="int"/>.</returns>
        private static int GetTotalChildcareAndHealthcareCosts(int plaintiffChildcareAndHealthcareCosts, int defendantChildcareAndHealthcareCosts)
        {
            return plaintiffChildcareAndHealthcareCosts + defendantChildcareAndHealthcareCosts;
        }

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
