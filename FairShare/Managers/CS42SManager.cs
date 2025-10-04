using FairShare.CustomObjects;
using FairShare.Models;
using FairShare.Seeds;

namespace FairShare.Managers
{
    public static class CS42SManager
    {
        public static int GetCombinedParentIncome(int plaintiffIncome, int defendantIncome)
        {
            return plaintiffIncome + defendantIncome;
        }

        public static int GetCombinedPreexistingChildSupportPayments(int plaintiffPreexistingChildSupport, int defendantPreexistingChildSupport)
        {
            return plaintiffPreexistingChildSupport + defendantPreexistingChildSupport;
        }

        public static int GetCombinedPreexistingAlimonyPayments(int plaintiffPreexistingAlimony, int defendantPreexistingAlimony)
        {
            return plaintiffPreexistingAlimony + defendantPreexistingAlimony;
        }

        public static int GetCombinedMonthlyAdjustedGrossIncome(int plaintiffAdjustedGrossIncome, int defendantAdjustedGrossIncome)
        {
            return plaintiffAdjustedGrossIncome + defendantAdjustedGrossIncome;
        }

        public static decimal GetPercentageShareOfIncome(int parentAdjustedGrossIncome, int combinedAdjustedGrossIncome)
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
        public static int GetSharedCustodyBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
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
        /// <param name="combinedAdjustedGrossIncome">The combined adjusted gross income of both parents, in whole dollars. Must be a non-negative integer.</param>
        /// <returns>The basic child support obligation, as an integer value in whole dollars.</returns>
        public static int GetBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
        {
            return BcsoLookup.Get(combinedAdjustedGrossIncome, numberOfChildren);
        }

        public static int GetTotalChildcareAndHealthcareCosts(int plaintiffChildcareAndHealthcareCosts, int defendantChildcareAndHealthcareCosts)
        {
            return plaintiffChildcareAndHealthcareCosts + defendantChildcareAndHealthcareCosts;
        }

        public static int GetTotalChildSupportObligation(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            int sharedBCSO = GetSharedCustodyBasicChildSupportObligation(
                numberOfChildren,
                GetCombinedMonthlyAdjustedGrossIncome(plaintiff.GetMonthlyAdjustedGrossIncome(), defendant.GetMonthlyAdjustedGrossIncome()));
            int totalChildcareAndHealthcareCosts = GetTotalChildcareAndHealthcareCosts(
                plaintiff.GetTotalChildcareAndHealthcareCosts(),
                defendant.GetTotalChildcareAndHealthcareCosts());
            return sharedBCSO + totalChildcareAndHealthcareCosts;
        }

        public static int GetSharedBcsoCredit(int numberOfChildren, int combinedAdjustedGrossIncome)
        {
            return (int)Math.Round(GetSharedCustodyBasicChildSupportObligation(numberOfChildren, combinedAdjustedGrossIncome) * 0.5, 0);
        }

        public static int GetAdjustedSharedChildSupportObligation(int parentChildSupportObligation, int parentTotalCostsPaid, int sharedBcsoCredit)
        {
            return parentChildSupportObligation - (parentTotalCostsPaid + sharedBcsoCredit);
        }

        public static FinalCalcWithPayerCO GetFinalCalculation(ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
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
            return (plaintiffFinalCalculation >= defendantFinalCalculation)
                ? new FinalCalcWithPayerCO("Plaintiff", plaintiffFinalCalculation)
                : new FinalCalcWithPayerCO("Defendant", plaintiffFinalCalculation);
        }
    }
}
