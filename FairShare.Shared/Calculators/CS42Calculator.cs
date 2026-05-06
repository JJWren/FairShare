using Microsoft.Extensions.Logging;
using FairShare.Shared.Helpers;
using FairShare.Shared.Models;
using static FairShare.Shared.Helpers.Enums;

namespace FairShare.Shared.Calculators
{
    /// <summary>
    /// The calculator for Form CS-42, currently implementing the standard custody guidelines as per Alabama state law.
    /// </summary>
    public class CS42Calculator(ILogger<CS42Calculator> logger) : BaseChildSupportCalculator
    {
        private readonly ILogger<CS42Calculator> _logger = logger;

        public override string State => States.AL.ToString();
        public override string Form => Forms.CS42.ToString();
        public override bool IsSharedCustody => false;

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

        private static int GetParentsRecommendedChildSupportObligation(int parentChildSupportObligation, int parentTotalCostsPaid)
        {
            int diff = parentChildSupportObligation - parentTotalCostsPaid;
            return diff <= 0 ? 0 : diff;
        }

        private static int GetParentsIncomeAvailableForChildSupport(int parentMonthlyGrossIncome)
            => parentMonthlyGrossIncome - 981;

        private static int GetMaxRecommendedChildSupportAmountAfterSSR(int parentIncomeAvailableForChildSupport)
        {
            int maxAmount = (int)Math.Round(parentIncomeAvailableForChildSupport * 0.85, 0);
            return maxAmount <= 0 ? 0 : maxAmount;
        }

        private static int GetFinalChildSupportAmount(int recommendedObligation, int maxObligationAfterSSR)
            => recommendedObligation < maxObligationAfterSSR ? recommendedObligation : maxObligationAfterSSR;
    }
}
