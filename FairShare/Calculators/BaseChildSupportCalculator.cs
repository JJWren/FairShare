using FairShare.Helpers;
using FairShare.Interfaces;
using FairShare.Models;
using FairShare.Seeds;

namespace FairShare.Calculators
{
    public abstract class BaseChildSupportCalculator : IChildSupportCalculator
    {
        public abstract string State { get; }
        public abstract string Form { get; }

        /// <summary>
        /// Shared custody flag; override in derived calculators that implement shared custody rules.
        /// </summary>
        public virtual bool IsSharedCustody => false;

        public abstract CalculationResult Calculate(ParentData plaintiff, ParentData defendant, int numberOfChildren);

        protected static int GetCombinedMonthlyAdjustedGrossIncome(int plaintiffAdjustedGrossIncome, int defendantAdjustedGrossIncome)
            => plaintiffAdjustedGrossIncome + defendantAdjustedGrossIncome;

        protected static decimal GetPercentageShareOfIncome(int parentAdjustedGrossIncome, int combinedAdjustedGrossIncome)
            => combinedAdjustedGrossIncome == 0 ? 0 : Math.Round((decimal)parentAdjustedGrossIncome / combinedAdjustedGrossIncome, 2);

        protected static int GetBasicChildSupportObligation(int numberOfChildren, int combinedAdjustedGrossIncome)
            => BcsoLookup.Get(combinedAdjustedGrossIncome, numberOfChildren);

        protected static int GetTotalChildcareAndHealthcareCosts(int plaintiffChildcareAndHealthcareCosts, int defendantChildcareAndHealthcareCosts)
            => plaintiffChildcareAndHealthcareCosts + defendantChildcareAndHealthcareCosts;

        protected CalculationResult CreateResultShell(int numberOfChildren)
            => new("N/A", 0)
            {
                Success = false,
                State = State,
                Form = Form,
                NumberOfChildren = numberOfChildren,
            };
    }
}
