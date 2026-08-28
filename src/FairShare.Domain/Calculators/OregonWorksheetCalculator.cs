using System;
using System.Collections.Generic;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;
using FairShare.Domain.Seeds;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// The Oregon Child Support Worksheet (CSF 02 0910, OAR 137-050-0700 to -0765), computed line
    /// by line with the official DOJ Guidelines Calculator workbook's own formulas - each block
    /// below names the worksheet line and mirrors that cell's formula, including where the workbook
    /// rounds (to 2 or 4 places, or to the dollar) and where it deliberately does not.
    /// The caretaker/state-care variant is not modeled (both-parents cases only).
    /// </summary>
    public sealed class OregonWorksheetCalculator(ILogger<OregonWorksheetCalculator>? logger = null) : IWorksheetForm
    {
        private readonly ILogger _logger = logger ?? NullLogger<OregonWorksheetCalculator>.Instance;

        public string State => States.OR.ToString();
        public string Form => Forms.Worksheet.ToString();
        public string DisplayName => "Child Support Worksheet (CSF 02 0910)";
        public string Description => "All custody arrangements; support to age 21 for Children Attending School";
        public bool IsSharedCustody => false;

        public OregonCalculationOutcome Calculate(OregonWorksheetInput input)
            => Calculate(input, OregonRuleParameters.Current);

        public OregonCalculationOutcome Calculate(OregonWorksheetInput input, OregonRuleParameters rules)
        {
            List<CalcError> errors = [];
            Validate(input, errors);

            if (errors.Count > 0)
            {
                return new OregonCalculationOutcome { Success = false, Errors = errors, RuleEffectiveDate = rules.EffectiveDate };
            }

            try
            {
                return CalculateValidated(input, rules, errors);
            }
            catch (Exception ex)
            {
                // The same envelope BaseChildSupportCalculator gives the Alabama forms: a
                // computation failure becomes a failed outcome, never an exception the API
                // would surface as an unhandled 500.
                _logger.LogError(ex, "Unexpected error in {Form} calculation.", Form);
                return new OregonCalculationOutcome
                {
                    Success = false,
                    Errors =
                    [
                        new CalcError
                        {
                            Code = CalcErrorCodes.UnexpectedError,
                            Message = "An unexpected error occurred during calculation.",
                            Field = null,
                            Severity = ErrorSeverity.Error,
                        }
                    ],
                    RuleEffectiveDate = rules.EffectiveDate,
                };
            }
        }

        private static OregonCalculationOutcome CalculateValidated(OregonWorksheetInput input, OregonRuleParameters rules, List<CalcError> errors)
        {
            OregonParentInput p = input.Plaintiff;
            OregonParentInput d = input.Defendant;
            int jointMinor = input.JointMinorChildren;
            int jointCas = input.JointChildrenAttendingSchool;
            int children = jointMinor + jointCas;

            // 1b: income after additions and subtractions - ROUND(income + spousalplus - spousalminus - uniondues - parentpremium, 2)
            decimal income1 = ExcelMath.Round(p.MonthlyIncome + p.SpousalSupportReceived - p.SpousalSupportPaid - p.UnionDues - p.OwnHealthInsuranceCost, 2);
            decimal income2 = ExcelMath.Round(d.MonthlyIncome + d.SpousalSupportReceived - d.SpousalSupportPaid - d.UnionDues - d.OwnHealthInsuranceCost, 2);

            // 1f: each parent's total children (non-joint + all joint)
            int totalCh1 = p.NonJointChildren + children;
            int totalCh2 = d.NonJointChildren + children;

            // 1g: non-joint child deduction - ROUND(scale(1b, 1f) / 1f * 1c, 2); the scale lookup
            // clamps more than ten children to the ten-child column
            decimal ded1 = p.NonJointChildren > 0 ? ExcelMath.Round(Scale(income1, totalCh1) / totalCh1 * p.NonJointChildren, 2) : 0m;
            decimal ded2 = d.NonJointChildren > 0 ? ExcelMath.Round(Scale(income2, totalCh2) / totalCh2 * d.NonJointChildren, 2) : 0m;

            // 1h: adjusted income - 1b minus 1g, floored at $0
            decimal adjusted1 = Math.Max(0m, income1 - ded1);
            decimal adjusted2 = Math.Max(0m, income2 - ded2);
            decimal adjustedC = adjusted1 + adjusted2;

            // 1i: income share - ROUND(adjusted / total, 4); each parent rounded independently
            decimal pct1 = adjustedC > 0 ? ExcelMath.Round(adjusted1 / adjustedC, 4) : 0m;
            decimal pct2 = adjustedC > 0 ? ExcelMath.Round(adjusted2 / adjustedC, 4) : 0m;

            // 1j: income available for support - ROUND(adjusted - SSR, 2), floored at $0
            decimal available1 = adjusted1 - rules.SelfSupportReserve < 0 ? 0m : ExcelMath.Round(adjusted1 - rules.SelfSupportReserve, 2);
            decimal available2 = adjusted2 - rules.SelfSupportReserve < 0 ? 0m : ExcelMath.Round(adjusted2 - rules.SelfSupportReserve, 2);

            // 2a: basic support obligation from the scale on total adjusted income and joint children
            decimal basicC = Scale(adjustedC, children);

            // 2b: lesser of (2a x income share, rounded to cents) or income available for support
            decimal basicSsr1 = basicC * pct1 < available1 ? ExcelMath.Round(basicC * pct1, 2) : available1;
            decimal basicSsr2 = basicC * pct2 < available2 ? ExcelMath.Round(basicC * pct2, 2) : available2;

            // 3a-3c: child care costs; the share (3c) is deliberately unrounded in the workbook
            decimal ccTotal = p.ChildCareCosts + d.ChildCareCosts;
            decimal availCc1 = available1 - basicSsr1;
            decimal availCc2 = available2 - basicSsr2;
            decimal ccShare1 = pct1 * ccTotal < availCc1 ? pct1 * ccTotal : availCc1;
            decimal ccShare2 = pct2 * ccTotal < availCc2 ? pct2 * ccTotal : availCc2;

            // 3d: support obligation after adding child care costs
            decimal csAfterCc1 = ExcelMath.Round(ccShare1 + basicSsr1, 2);
            decimal csAfterCc2 = ExcelMath.Round(ccShare2 + basicSsr2, 2);

            // 4b: income available for health care coverage
            decimal availHcc1 = available1 - csAfterCc1;
            decimal availHcc2 = available2 - csAfterCc2;

            // 4c: reasonable cost - lesser of 4% of adjusted income or 4b, to the dollar; $0 for a
            // parent whose income (1a) is at or below full-time highest Oregon minimum wage
            decimal ric1 = p.MonthlyIncome > rules.HighestMinimumWageMonthly
                ? ExcelMath.Round(Math.Min(adjusted1 * rules.MedicalCostCapRate, availHcc1), 0)
                : 0m;
            decimal ric2 = d.MonthlyIncome > rules.HighestMinimumWageMonthly
                ? ExcelMath.Round(Math.Min(adjusted2 * rules.MedicalCostCapRate, availHcc2), 0)
                : 0m;
            decimal ricTotal = ExcelMath.Round(ric1 + ric2, 0);

            // 4d: whose coverage is available at a reasonable cost (the workbook's hidden option table)
            bool higher = input.OrderCoverageAtHigherAmount;
            bool can1 = CanProvide(p, ricTotal, rules, higher);
            bool can2 = CanProvide(d, ricTotal, rules, higher);
            bool canBoth = p.ChildrensHealthCoverageCost is decimal prem1 && d.ChildrensHealthCoverageCost is decimal prem2
                && (p.MonthlyIncome > rules.HighestMinimumWageMonthly || prem1 == 0)
                && (d.MonthlyIncome > rules.HighestMinimumWageMonthly || prem2 == 0)
                && (prem1 + prem2 <= ricTotal || higher);

            // 4f: who will provide - explicit selection validated against 4d, otherwise the rule's
            // default (only qualifying parent; both qualify -> more parenting time, tie -> cheaper)
            CoverageProvider? provider = ResolveProvider(input, can1, can2, canBoth, errors);
            if (provider is null)
            {
                return new OregonCalculationOutcome { Success = false, Errors = errors, RuleEffectiveDate = rules.EffectiveDate };
            }

            decimal hccTotal = provider switch
            {
                CoverageProvider.Both => (p.ChildrensHealthCoverageCost ?? 0m) + (d.ChildrensHealthCoverageCost ?? 0m),
                CoverageProvider.Plaintiff => p.ChildrensHealthCoverageCost ?? 0m,
                CoverageProvider.Defendant => d.ChildrensHealthCoverageCost ?? 0m,
                _ => 0m,
            };

            // 4g: each parent's percentage share of coverage costs - ROUND(4c / 4c total, 4)
            decimal hccPct1 = ricTotal > 0 ? ExcelMath.Round(ric1 / ricTotal, 4) : 0m;
            decimal hccPct2 = ricTotal > 0 ? ExcelMath.Round(ric2 / ricTotal, 4) : 0m;

            // 4h: each parent's share of the ordered coverage cost
            decimal hccShare1 = ExcelMath.Round(hccTotal * hccPct1, 2);
            decimal hccShare2 = ExcelMath.Round(hccTotal * hccPct2, 2);

            // 4i: support obligation after adding health care coverage costs
            decimal csAfterHcc1 = csAfterCc1 + hccShare1;
            decimal csAfterHcc2 = csAfterCc2 + hccShare2;

            // 5b: cash medical support - the reasonable-cost amount when elected ("y" or "c")
            decimal cmeds1 = input.CashMedical == CashMedicalElection.No ? 0m : ric1;
            decimal cmeds2 = input.CashMedical == CashMedicalElection.No ? 0m : ric2;

            // 6b: parenting time credit percentage - the OAR 137-050-0730 logistic on overnights/365,
            // computed in doubles exactly as Excel evaluates EXP
            decimal ptPct1 = ParentingTimeCreditPercent(p.AverageOvernights);
            decimal ptPct2 = ParentingTimeCreditPercent(d.AverageOvernights);

            // 6c: parenting time credit - 2a x (minors / all joint children) x 6b, to cents;
            // Children Attending School are excluded from the credit base
            decimal ptCred1 = ExcelMath.Round(basicC * jointMinor / children * ptPct1, 2);
            decimal ptCred2 = ExcelMath.Round(basicC * jointMinor / children * ptPct2, 2);

            // 6d/6e: credits for own child care outlay and own premium when providing coverage
            decimal ccCred1 = p.ChildCareCosts;
            decimal ccCred2 = d.ChildCareCosts;
            decimal hccCred1 = provider is CoverageProvider.Plaintiff or CoverageProvider.Both ? p.ChildrensHealthCoverageCost ?? 0m : 0m;
            decimal hccCred2 = provider is CoverageProvider.Defendant or CoverageProvider.Both ? d.ChildrensHealthCoverageCost ?? 0m : 0m;

            // 6f: support after credits - may be negative
            decimal csAfterCred1 = csAfterHcc1 - (ptCred1 + ccCred1 + hccCred1);
            decimal csAfterCred2 = csAfterHcc2 - (ptCred2 + ccCred2 + hccCred2);

            // 7a: minor children's portion of the basic support obligation - deliberately unrounded
            decimal minorsBs1 = basicSsr1 / children * jointMinor;
            decimal minorsBs2 = basicSsr2 / children * jointMinor;

            // 7b: net obligation for minor children - the payer comparison figure
            decimal net1 = minorsBs1 + ccShare1 + (hccShare1 / children * jointMinor) - ptCred1 - ccCred1 - (hccCred1 / children * jointMinor);
            decimal net2 = minorsBs2 + ccShare2 + (hccShare2 / children * jointMinor) - ptCred2 - ccCred2 - (hccCred2 / children * jointMinor);

            // 7c: the parent with the higher net obligation pays for the minors; equal figures or no
            // minor children means neither does
            bool pays1 = net1 != net2 && jointMinor >= 1 && net1 > net2;
            bool pays2 = net1 != net2 && jointMinor >= 1 && net2 > net1;

            // 8a: total support payment including medical - add the greater of the coverage credit
            // (6e) or cash medical (5b)
            decimal before1 = hccCred1 > cmeds1 ? hccCred1 + csAfterCred1 : csAfterCred1 + cmeds1;
            decimal before2 = hccCred2 > cmeds2 ? hccCred2 + csAfterCred2 : csAfterCred2 + cmeds2;

            // 8c: top-up to the $100 minimum order unless the parent has an exception (8b)
            decimal add1 = p.MinimumOrderException ? 0m : before1 < rules.MinimumOrder ? rules.MinimumOrder - before1 : 0m;
            decimal add2 = d.MinimumOrderException ? 0m : before2 < rules.MinimumOrder ? rules.MinimumOrder - before2 : 0m;

            // 8d: cash child support after the minimum order; a parent who should not pay for the
            // minors owes nothing here unless there are Children Attending School
            decimal afterMin1 = !pays1 && jointCas < 1 ? 0m : Math.Max(0m, ExcelMath.Round(csAfterCred1 + add1, 2));
            decimal afterMin2 = !pays2 && jointCas < 1 ? 0m : Math.Max(0m, ExcelMath.Round(csAfterCred2 + add2, 2));

            // 8f: dollar-for-dollar reduction for Social Security / veterans benefits (8e)
            decimal afterSsv1 = Math.Max(afterMin1 - p.SocialSecurityVeteransBenefits, 0m);
            decimal afterSsv2 = Math.Max(afterMin2 - d.SocialSecurityVeteransBenefits, 0m);

            // 8g: benefits in excess of cash support carry over against cash medical
            decimal remRed1 = afterSsv1 == afterMin1 - p.SocialSecurityVeteransBenefits ? 0m : Math.Abs(afterMin1 - p.SocialSecurityVeteransBenefits);
            decimal remRed2 = afterSsv2 == afterMin2 - d.SocialSecurityVeteransBenefits ? 0m : Math.Abs(afterMin2 - d.SocialSecurityVeteransBenefits);

            // 8h: cash medical after the remaining reduction
            decimal cmsAfter1 = Math.Max(cmeds1 - remRed1, 0m);
            decimal cmsAfter2 = Math.Max(cmeds2 - remRed2, 0m);

            // 9a-9d: prorate cash support and cash medical between the minors and the Children
            // Attending School, each piece rounded to the dollar the way the workbook nests it
            decimal csMinor1 = !pays1 ? 0m : ExcelMath.Round(ExcelMath.Round(afterSsv1 / children, 2) * jointMinor, 0);
            decimal csMinor2 = !pays2 ? 0m : ExcelMath.Round(ExcelMath.Round(afterSsv2 / children, 2) * jointMinor, 0);
            decimal cmedsMinor1 = !pays1 ? 0m : ExcelMath.Round(ExcelMath.Round(cmsAfter1 / children, 2) * jointMinor, 0);
            decimal cmedsMinor2 = !pays2 ? 0m : ExcelMath.Round(ExcelMath.Round(cmsAfter2 / children, 2) * jointMinor, 0);
            decimal csCas1 = !pays1
                ? jointCas > 0 ? ExcelMath.Round(afterSsv1, 0) : 0m
                : ExcelMath.Round(ExcelMath.Round(afterSsv1 / children, 2) * jointCas, 0);
            decimal csCas2 = !pays2
                ? jointCas > 0 ? ExcelMath.Round(afterSsv2, 0) : 0m
                : ExcelMath.Round(ExcelMath.Round(afterSsv2 / children, 2) * jointCas, 0);
            decimal cmedsCas1 = jointCas > 0
                ? !pays1 || jointMinor == 0 ? ExcelMath.Round(cmsAfter1, 0) : ExcelMath.Round(ExcelMath.Round(cmsAfter1 / children, 2) * jointCas, 0)
                : 0m;
            decimal cmedsCas2 = jointCas > 0
                ? !pays2 || jointMinor == 0 ? ExcelMath.Round(cmsAfter2, 0) : ExcelMath.Round(ExcelMath.Round(cmsAfter2 / children, 2) * jointCas, 0)
                : 0m;

            // 9e: total child support per parent
            decimal total1 = csMinor1 + cmedsMinor1 + csCas1 + cmedsCas1;
            decimal total2 = csMinor2 + cmedsMinor2 + csCas2 + cmedsCas2;

            // 9g: the reasonable cost to name in the order - the greater of 4c and 4f when coverage
            // is ordered at a higher amount
            decimal ric9g = higher ? Math.Max(ricTotal, hccTotal) : ricTotal;

            WorksheetBuilder w = new();
            w.Add("1a", "Income", p.MonthlyIncome, d.MonthlyIncome)
             .Add("1b", "Income after additions and subtractions", income1, income2)
             .Add("1c", "Number of non-joint children", p.NonJointChildren, d.NonJointChildren, format: LineFormat.Number)
             .Add("1d", "Number of joint minor children", combined: jointMinor, format: LineFormat.Number)
             .Add("1e", "Number of joint Children Attending School age 18 to 20", combined: jointCas, format: LineFormat.Number)
             .Add("1f", "Total number of children", totalCh1, totalCh2, format: LineFormat.Number)
             .Add("1g", "Non-joint child deduction", ded1, ded2)
             .Add("1h", "Adjusted income", adjusted1, adjusted2, adjustedC)
             .Add("1i", "Each parent's income share percentage", pct1, pct2, format: LineFormat.Percent)
             .Add("1j", "Income available for support", available1, available2)
             .Add("2a", "Basic support obligation (from obligation scale)", combined: basicC)
             .Add("2b", "Basic support obligation after self-support reserve", basicSsr1, basicSsr2)
             .Add("3a", "Child care costs for joint children under 13 or disabled", p.ChildCareCosts, d.ChildCareCosts)
             .Add("3b", "Income available for child care costs", availCc1, availCc2)
             .Add("3c", "Parents' shares of child care costs", ccShare1, ccShare2)
             .Add("3d", "Support obligation after adding child care costs", csAfterCc1, csAfterCc2)
             .Add("4a", "Health care coverage costs for joint children", p.ChildrensHealthCoverageCost, d.ChildrensHealthCoverageCost)
             .Add("4b", "Income available for health care coverage", availHcc1, availHcc2)
             .Add("4c", "Reasonable cost for health care coverage", ric1, ric2, ricTotal)
             .Add("4f", "Health care coverage that will be ordered", combined: hccTotal)
             .Add("4g", "Parents' percentage share of health care coverage costs", hccPct1, hccPct2, format: LineFormat.Percent)
             .Add("4h", "Each parent's share of health care coverage costs", hccShare1, hccShare2)
             .Add("4i", "Support obligation after adding health care coverage costs", csAfterHcc1, csAfterHcc2)
             .Add("5b", "Cash medical support amount", cmeds1, cmeds2)
             .Add("6a", "Average number of overnights (or equivalent)", p.AverageOvernights, d.AverageOvernights, format: LineFormat.Number)
             .Add("6b", "Parenting time credit percentage", ptPct1, ptPct2, format: LineFormat.Percent)
             .Add("6c", "Parenting time credit", ptCred1, ptCred2)
             .Add("6d", "Child care credit", ccCred1, ccCred2)
             .Add("6e", "Credit for health care coverage costs", hccCred1, hccCred2)
             .Add("6f", "Support after credits", csAfterCred1, csAfterCred2)
             .Add("7a", "Minor children's portion of basic support obligation", minorsBs1, minorsBs2)
             .Add("7b", "Net obligation for minor children", net1, net2)
             .Add("8a", "Total support payment obligation, including medical support", before1, before2)
             .Add("8c", "Amount needed to meet minimum order", add1, add2)
             .Add("8d", "Cash child support obligation after minimum order", afterMin1, afterMin2)
             .Add("8e", "Reduction for Social Security or veterans benefits", p.SocialSecurityVeteransBenefits, d.SocialSecurityVeteransBenefits)
             .Add("8f", "Cash child support after Social Security or veterans benefits", afterSsv1, afterSsv2)
             .Add("8g", "Remaining reduction to apply to cash medical support", remRed1, remRed2)
             .Add("8h", "Cash medical support after Social Security or veterans benefits", cmsAfter1, cmsAfter2)
             .Add("9a", "Cash child support for minor children", csMinor1, csMinor2)
             .Add("9b", "Cash medical support for minor children", cmedsMinor1, cmedsMinor2)
             .Add("9c", "Cash child support for Children Attending School", csCas1, csCas2)
             .Add("9d", "Cash medical support for Children Attending School", cmedsCas1, cmedsCas2)
             .Add("9e", "Total child support", total1, total2)
             .Add("9g", "Reasonable cost for health care coverage", combined: ric9g);

            return new OregonCalculationOutcome
            {
                Success = true,
                Errors = errors,
                Lines = w.Build(),
                PaysForMinorChildren = pays1 ? ParentType.Plaintiff : pays2 ? ParentType.Defendant : null,
                PlaintiffTotalSupport = total1,
                DefendantTotalSupport = total2,
                CoverageProvider = provider.Value,
                ReasonableCostTotal = ric9g,
                RuleEffectiveDate = rules.EffectiveDate,
            };
        }

        /// <summary>
        /// The scale amount for a (possibly fractional, possibly negative) income: flooring to a
        /// whole dollar preserves the "equal to or greater than" bracket the workbook's VLOOKUP finds.
        /// </summary>
        private static decimal Scale(decimal combinedAdjustedIncome, int children)
            => OregonScaleLookup.Get((int)decimal.Floor(combinedAdjustedIncome), children);

        /// <summary>
        /// Line 6b: <c>ROUND(1/(1+EXP(-7.14*(t-0.5))) - 2.74% + 2*2.74%*t, 4)</c> with
        /// t = overnights/365, evaluated in doubles exactly as Excel does. Exactly 0% at zero
        /// overnights and 100% at 365.
        /// </summary>
        private static decimal ParentingTimeCreditPercent(decimal overnights)
        {
            double t = (double)overnights / 365d;
            double credit = 1d / (1d + Math.Exp(-7.14d * (t - 0.5d))) - 0.0274d + 2d * 0.0274d * t;
            return ExcelMath.Round((decimal)credit, 4);
        }

        /// <summary>
        /// Line 4d for one parent: coverage counts as available at a reasonable cost when a premium
        /// is stated (not "none"), the parent earns above full-time highest minimum wage or the
        /// coverage is free, and the premium fits the reasonable-cost total (unless coverage is
        /// ordered at a higher amount).
        /// </summary>
        private static bool CanProvide(OregonParentInput parent, decimal ricTotal, OregonRuleParameters rules, bool higher)
            => parent.ChildrensHealthCoverageCost is decimal premium
                && (parent.MonthlyIncome > rules.HighestMinimumWageMonthly || premium == 0)
                && (premium <= ricTotal || higher);

        private static CoverageProvider? ResolveProvider(OregonWorksheetInput input, bool can1, bool can2, bool canBoth, List<CalcError> errors)
        {
            if (input.CoverageSelection is CoverageProvider selected)
            {
                bool valid = selected switch
                {
                    CoverageProvider.Plaintiff => can1,
                    CoverageProvider.Defendant => can2,
                    CoverageProvider.Both => canBoth,
                    CoverageProvider.EitherWhenAvailable => !can1 && !can2,
                    _ => false,
                };

                if (!valid)
                {
                    errors.Add(new CalcError
                    {
                        Code = CalcErrorCodes.CoverageSelectionUnavailable,
                        Message = "The selected health-care-coverage provider is not among the options available at a reasonable cost (worksheet line 4d).",
                        Field = nameof(input.CoverageSelection),
                        Severity = ErrorSeverity.Error,
                    });
                    return null;
                }

                return selected;
            }

            // OAR 137-050-0750: only one parent qualifies -> that parent; both qualify -> the parent
            // with more parenting time selects (we assume their own coverage), ties -> the cheaper.
            if (can1 && can2)
            {
                if (input.Plaintiff.AverageOvernights != input.Defendant.AverageOvernights)
                {
                    return input.Plaintiff.AverageOvernights > input.Defendant.AverageOvernights
                        ? CoverageProvider.Plaintiff
                        : CoverageProvider.Defendant;
                }

                return (input.Plaintiff.ChildrensHealthCoverageCost ?? 0m) <= (input.Defendant.ChildrensHealthCoverageCost ?? 0m)
                    ? CoverageProvider.Plaintiff
                    : CoverageProvider.Defendant;
            }

            return can1 ? CoverageProvider.Plaintiff
                : can2 ? CoverageProvider.Defendant
                : CoverageProvider.EitherWhenAvailable;
        }

        private static void Validate(OregonWorksheetInput input, List<CalcError> errors)
        {
            void Negative(string field) => errors.Add(new CalcError
            {
                Code = CalcErrorCodes.NegativeInput,
                Message = $"{field} cannot be negative.",
                Field = field,
                Severity = ErrorSeverity.Error,
            });

            foreach ((OregonParentInput parent, string side) in new[] { (input.Plaintiff, "Plaintiff"), (input.Defendant, "Defendant") })
            {
                if (parent.MonthlyIncome < 0) Negative($"{side}.{nameof(parent.MonthlyIncome)}");
                if (parent.SpousalSupportReceived < 0) Negative($"{side}.{nameof(parent.SpousalSupportReceived)}");
                if (parent.SpousalSupportPaid < 0) Negative($"{side}.{nameof(parent.SpousalSupportPaid)}");
                if (parent.UnionDues < 0) Negative($"{side}.{nameof(parent.UnionDues)}");
                if (parent.OwnHealthInsuranceCost < 0) Negative($"{side}.{nameof(parent.OwnHealthInsuranceCost)}");
                if (parent.NonJointChildren < 0) Negative($"{side}.{nameof(parent.NonJointChildren)}");
                if (parent.ChildCareCosts < 0) Negative($"{side}.{nameof(parent.ChildCareCosts)}");
                if (parent.ChildrensHealthCoverageCost < 0) Negative($"{side}.{nameof(parent.ChildrensHealthCoverageCost)}");
                if (parent.AverageOvernights < 0) Negative($"{side}.{nameof(parent.AverageOvernights)}");
                if (parent.SocialSecurityVeteransBenefits < 0) Negative($"{side}.{nameof(parent.SocialSecurityVeteransBenefits)}");
            }

            if (input.JointMinorChildren < 0) Negative(nameof(input.JointMinorChildren));
            if (input.JointChildrenAttendingSchool < 0) Negative(nameof(input.JointChildrenAttendingSchool));

            if (errors.Count > 0)
            {
                return;
            }

            if (input.JointMinorChildren + input.JointChildrenAttendingSchool < 1)
            {
                errors.Add(new CalcError
                {
                    Code = CalcErrorCodes.InvalidChildCount,
                    Message = "Enter at least one joint child (minor or Child Attending School).",
                    Field = nameof(input.JointMinorChildren),
                    Severity = ErrorSeverity.Error,
                });
                return;
            }

            if (input.JointMinorChildren > 0
                && input.Plaintiff.AverageOvernights + input.Defendant.AverageOvernights != 365m)
            {
                errors.Add(new CalcError
                {
                    Code = CalcErrorCodes.OvernightsMustTotal365,
                    Message = "The parents' average overnights must total 365 (worksheet line 6a).",
                    // A cross-field rule - no single dotted field path is actionable here.
                    Field = null,
                    Severity = ErrorSeverity.Error,
                });
            }
        }
    }
}
