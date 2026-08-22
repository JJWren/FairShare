using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;
using FairShare.Domain.Seeds;
using Microsoft.Extensions.Logging;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// Alabama Form CS-42 (Rev. 5/2022), the standard-custody worksheet. Every line below is the matching line of the
    /// official AOC workbook, computed with the workbook's own formula.
    /// </summary>
    public class CS42Calculator(ILogger<CS42Calculator> logger) : BaseChildSupportCalculator(logger)
    {
        /// <summary>Line 11: the self-support reserve subtracted from each parent's adjusted gross income.</summary>
        public const int SelfSupportReserve = 981;

        /// <summary>Line 12: the share of income above the reserve that is available for support.</summary>
        public const decimal AvailableIncomeRate = 0.85m;

        /// <summary>Line 12: the minimum obligation a parent with income is assigned.</summary>
        public const int MinimumObligation = 50;

        public override string State => States.AL.ToString();
        public override string Form => Forms.CS42.ToString();
        public override string DisplayName => "CS-42 (Rev. 5/2022)";
        public override string Description => "Standard custody";
        public override bool IsSharedCustody => false;

        protected override IObligationSchedule Schedule => AlabamaObligationSchedule.Instance;

        protected override WorksheetOutcome BuildWorksheet(WorksheetBuilder worksheet, ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            // Lines 1, 1a, 1b, 2
            IncomeLines income = AddIncomeLines(worksheet, plaintiff, defendant,
                "Minus Preexisting Child Support Payments",
                "Minus Preexisting Periodic Alimony Payments");

            // Line 3 - the workbook rounds the plaintiff's share and gives the defendant the remainder (=1-H18).
            decimal plaintiffShare = ShareOfIncome(income.PlaintiffAdjusted, income.CombinedAdjusted);
            decimal defendantShare = income.CombinedAdjusted == 0 ? 0m : 1m - plaintiffShare;
            worksheet.Add("3", "PERCENTAGE SHARE OF INCOME", plaintiffShare, defendantShare, plaintiffShare + defendantShare, LineFormat.Percent);

            // Line 4 - schedule lookup on the combined adjusted gross income.
            int basicObligation = GetBasicChildSupportObligation(numberOfChildren, income.CombinedAdjusted);
            worksheet.Add("4", "BASIC CHILD SUPPORT OBLIGATION", combined: basicObligation);

            // Lines 5, 6
            int combinedChildcare = plaintiff.WorkRelatedChildcareCosts + defendant.WorkRelatedChildcareCosts;
            int combinedHealthcare = plaintiff.HealthcareCoverageCosts + defendant.HealthcareCoverageCosts;
            worksheet
                .Add("5", "WORK-RELATED CHILD-CARE COSTS", plaintiff.WorkRelatedChildcareCosts, defendant.WorkRelatedChildcareCosts, combinedChildcare)
                .Add("6", "HEALTH-CARE-COVERAGE COSTS", plaintiff.HealthcareCoverageCosts, defendant.HealthcareCoverageCosts, combinedHealthcare);

            // Line 7 - combined line 4 + line 5 + line 6.
            int totalObligation = basicObligation + combinedChildcare + combinedHealthcare;
            worksheet.Add("7", "TOTAL CHILD-SUPPORT OBLIGATION", combined: totalObligation);

            // Line 8 - the workbook rounds the plaintiff's share and gives the defendant the remainder (=L22-H23).
            int plaintiffObligation = ExcelRound(plaintiffShare * totalObligation);
            int defendantObligation = totalObligation - plaintiffObligation;
            worksheet.Add("8", "EACH PARENT'S CHILD SUPPORT OBLIGATION", plaintiffObligation, defendantObligation);

            // Line 9 - line 5 + line 6 for each parent.
            int plaintiffCostsPaid = plaintiff.GetTotalChildcareAndHealthcareCosts();
            int defendantCostsPaid = defendant.GetTotalChildcareAndHealthcareCosts();
            worksheet.Add("9", "TOTAL COSTS PAID BY EACH PARENT", plaintiffCostsPaid, defendantCostsPaid);

            // Line 10 - line 8 - line 9, not below $0.
            int plaintiffAdjustedObligation = Math.Max(plaintiffObligation - plaintiffCostsPaid, 0);
            int defendantAdjustedObligation = Math.Max(defendantObligation - defendantCostsPaid, 0);
            worksheet.Add("10", "EACH PARENT'S ADJUSTED CHILD-SUPPORT OBLIGATION", plaintiffAdjustedObligation, defendantAdjustedObligation);

            // Line 11 - line 2 (adjusted, not gross) minus the self-support reserve, not below $0.
            int plaintiffAfterReserve = Math.Max(income.PlaintiffAdjusted - SelfSupportReserve, 0);
            int defendantAfterReserve = Math.Max(income.DefendantAdjusted - SelfSupportReserve, 0);
            worksheet.Add("11", "INCOME AVAILABLE AFTER SSR", plaintiffAfterReserve, defendantAfterReserve);

            // Line 12 - 85% of line 11, never below the $50 minimum obligation.
            int plaintiffAvailable = Math.Max(ExcelRound(AvailableIncomeRate * plaintiffAfterReserve), MinimumObligation);
            int defendantAvailable = Math.Max(ExcelRound(AvailableIncomeRate * defendantAfterReserve), MinimumObligation);
            worksheet.Add("12", "INCOME AVAILABLE FOR SUPPORT", plaintiffAvailable, defendantAvailable);

            // Line 13 - lesser of lines 10 and 12; a parent with no gross income at all owes $0 (=IF(H14=0,0,...)).
            int plaintiffOrder = income.PlaintiffGross == 0 ? 0 : Math.Min(plaintiffAdjustedObligation, plaintiffAvailable);
            int defendantOrder = income.DefendantGross == 0 ? 0 : Math.Min(defendantAdjustedObligation, defendantAvailable);
            worksheet.Add("13", "RECOMMENDED CHILD-SUPPORT ORDER", plaintiffOrder, defendantOrder);

            // The form shows both columns; the order applies to the parent without primary custody.
            return plaintiff.HasPrimaryCustody
                ? new WorksheetOutcome(ParentType.Defendant.ToString(), defendantOrder)
                : new WorksheetOutcome(ParentType.Plaintiff.ToString(), plaintiffOrder);
        }
    }
}
