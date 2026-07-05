using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// The calculator for Form CS-42-S, currently implementing the shared custody guidelines as per Alabama state law.
    /// </summary>
    public class CS42SCalculator(ILogger<CS42SCalculator> logger) : BaseChildSupportCalculator
    {
        private readonly ILogger<CS42SCalculator> _logger = logger;

        public override string State => States.AL.ToString();
        public override string Form => Forms.CS42S.ToString();
        public override bool IsSharedCustody => true;

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

                int basicChildSupportObligation = GetBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome);
                int sharedParentingObligation = (int)Math.Round(basicChildSupportObligation * 1.5, 0);

                decimal plaintiffIncomePercentage = GetPercentageShareOfIncome(
                    plaintiff.GetMonthlyAdjustedGrossIncome(),
                    combinedAdjustedGrossIncome);
                decimal defendantIncomePercentage = GetPercentageShareOfIncome(
                    defendant.GetMonthlyAdjustedGrossIncome(),
                    combinedAdjustedGrossIncome);

                int plaintiffSharedParentingObligation = (int)Math.Round(sharedParentingObligation * plaintiffIncomePercentage, 0);
                int defendantSharedParentingObligation = (int)Math.Round(sharedParentingObligation * defendantIncomePercentage, 0);

                int plaintiffBasicObligation = (int)Math.Round(defendantSharedParentingObligation * 0.5, 0);
                int defendantBasicObligation = (int)Math.Round(plaintiffSharedParentingObligation * 0.5, 0);

                int plaintiffTotalCostsPaid = plaintiff.GetTotalChildcareAndHealthcareCosts();
                int defendantTotalCostsPaid = defendant.GetTotalChildcareAndHealthcareCosts();

                int plaintiffRecommendedObligation = plaintiffBasicObligation - (int)Math.Round(plaintiffTotalCostsPaid * defendantIncomePercentage, 0);
                int defendantRecommendedObligation = defendantBasicObligation - (int)Math.Round(defendantTotalCostsPaid * plaintiffIncomePercentage, 0);

                if (plaintiffRecommendedObligation > defendantRecommendedObligation)
                {
                    result.Payer = Enums.ParentType.Plaintiff.ToString();
                    result.FinalAmount = plaintiffRecommendedObligation - defendantRecommendedObligation;
                }
                else
                {
                    result.Payer = Enums.ParentType.Defendant.ToString();
                    result.FinalAmount = defendantRecommendedObligation - plaintiffRecommendedObligation;
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
    }
}






