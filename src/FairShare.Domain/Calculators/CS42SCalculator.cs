using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// Alabama Form CS-42-S (Eff. 6/2023), the shared 50% physical-custody worksheet. Every line below is the matching
    /// line of the official AOC workbook, computed with the workbook's own formula. The form assumes 50/50 custody, so
    /// the primary-custody flag is ignored.
    /// </summary>
    public class CS42SCalculator(ILogger<CS42SCalculator> logger) : BaseChildSupportCalculator(logger)
    {
        /// <summary>Line 5: the shared-custody obligation is 150% of the basic obligation.</summary>
        public const decimal SharedCustodyMultiplier = 1.5m;

        /// <summary>Line 12: each parent is credited half of the shared-custody obligation for the time the children are with them.</summary>
        public const decimal SharedCustodyCreditRate = 0.5m;

        public override string State => States.AL.ToString();
        public override string Form => Forms.CS42S.ToString();
        public override string DisplayName => "CS-42-S (Eff. 6/2023)";
        public override string Description => "Shared 50/50 physical custody";
        public override bool IsSharedCustody => true;

        protected override WorksheetOutcome BuildWorksheet(WorksheetBuilder worksheet, ParentData plaintiff, ParentData defendant, int numberOfChildren)
        {
            // Lines 1, 1a, 1b, 2
            IncomeLines income = AddIncomeLines(worksheet, plaintiff, defendant,
                "Minus Preexisting Child-Support Payments",
                "Minus Preexisting Periodic Alimony Payments");

            // Line 3 - unlike CS-42, this workbook rounds the DEFENDANT's share and gives the plaintiff the remainder (=1-J19).
            decimal defendantShare = ShareOfIncome(income.DefendantAdjusted, income.CombinedAdjusted);
            decimal plaintiffShare = income.CombinedAdjusted == 0 ? 0m : 1m - defendantShare;
            worksheet.Add("3", "PERCENTAGE SHARE OF INCOME", plaintiffShare, defendantShare, plaintiffShare + defendantShare, LineFormat.Percent);

            // Line 4 - schedule lookup on the combined adjusted gross income.
            int basicObligation = GetBasicChildSupportObligation(numberOfChildren, income.CombinedAdjusted);
            worksheet.Add("4", "BASIC CHILD-SUPPORT OBLIGATION", combined: basicObligation);

            // Line 5 - 150% of line 4.
            int sharedObligation = ExcelRound(SharedCustodyMultiplier * basicObligation);
            worksheet.Add("5", "SHARED 50% PHYSICAL-CUSTODY CHILD-SUPPORT OBLIGATION", combined: sharedObligation);

            // Lines 6, 7, 8 - costs per parent; line 8 is the only one with a combined cell.
            int plaintiffCosts = plaintiff.GetTotalChildcareAndHealthcareCosts();
            int defendantCosts = defendant.GetTotalChildcareAndHealthcareCosts();
            int combinedCosts = plaintiffCosts + defendantCosts;
            worksheet
                .Add("6", "WORK-RELATED CHILD-CARE COSTS", plaintiff.WorkRelatedChildcareCosts, defendant.WorkRelatedChildcareCosts)
                .Add("7", "HEALTH-CARE-COVERAGE COSTS", plaintiff.HealthcareCoverageCosts, defendant.HealthcareCoverageCosts)
                .Add("8", "TOTAL CHILD-CARE AND HEALTH-CARE-COVERAGE COSTS", plaintiffCosts, defendantCosts, combinedCosts);

            // Line 9 - combined line 5 + line 8.
            int totalObligation = sharedObligation + combinedCosts;
            worksheet.Add("9", "TOTAL CHILD-SUPPORT OBLIGATION", combined: totalObligation);

            // Line 10 - line 3 x line 9, each column rounded on its own.
            int plaintiffObligation = ExcelRound(plaintiffShare * totalObligation);
            int defendantObligation = ExcelRound(defendantShare * totalObligation);
            worksheet.Add("10", "EACH PARENT'S CHILD-SUPPORT OBLIGATION", plaintiffObligation, defendantObligation);

            // Line 11 - line 8 for each parent.
            worksheet.Add("11", "TOTAL COSTS PAID BY EACH PARENT", plaintiffCosts, defendantCosts);

            // Line 12 - 50% of line 5 combined, the same figure in both columns.
            int sharedCustodyCredit = ExcelRound(SharedCustodyCreditRate * sharedObligation);
            worksheet.Add("12", "CREDIT FOR SHARED 50% PHYSICAL CUSTODY", sharedCustodyCredit, sharedCustodyCredit);

            // Line 13 - line 10 - line 11 - line 12; the lower earner's column normally goes negative.
            int plaintiffAdjustedObligation = plaintiffObligation - plaintiffCosts - sharedCustodyCredit;
            int defendantAdjustedObligation = defendantObligation - defendantCosts - sharedCustodyCredit;
            worksheet.Add("13", "ADJUSTED SHARED 50% PHYSICAL-CUSTODY CHILD-SUPPORT OBLIGATION", plaintiffAdjustedObligation, defendantAdjustedObligation);

            // Line 14 - the higher line-13 amount, placed in that parent's column (ties go to the defendant, =IF(J30>=H30,...)).
            bool plaintiffPays = plaintiffAdjustedObligation > defendantAdjustedObligation;
            int order = plaintiffPays ? plaintiffAdjustedObligation : defendantAdjustedObligation;
            worksheet.Add("14", "RECOMMENDED CHILD-SUPPORT ORDER",
                plaintiffPays ? order : null,
                plaintiffPays ? null : order);

            // A zero (or, after rounding, negative) order is "no net transfer": an empty payer is the UI's signal
            // for that - naming a parent with a $0 amount would read as a real (if empty) obligation.
            if (order <= 0)
            {
                return new WorksheetOutcome(string.Empty, 0);
            }

            return plaintiffPays
                ? new WorksheetOutcome(ParentType.Plaintiff.ToString(), order)
                : new WorksheetOutcome(ParentType.Defendant.ToString(), order);
        }
    }
}
