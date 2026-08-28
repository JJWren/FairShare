using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using FairShare.Domain.Seeds;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Tests.Domain;

public class OregonWorksheetCalculatorTests
{
    private static readonly OregonWorksheetCalculator Calculator = new();

    private static OregonWorksheetInput Input(
        OregonParentInput? plaintiff = null,
        OregonParentInput? defendant = null,
        int minors = 1,
        int cas = 0,
        CoverageProvider? selection = null)
        => new()
        {
            Plaintiff = plaintiff ?? new OregonParentInput { MonthlyIncome = 4500, AverageOvernights = 91 },
            Defendant = defendant ?? new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 274 },
            JointMinorChildren = minors,
            JointChildrenAttendingSchool = cas,
            CoverageSelection = selection,
        };

    [Fact]
    public void Calculate_NoJointChildren_ReturnsInvalidChildCount()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(minors: 0, cas: 0));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Errors, e => e.Code == CalcErrorCodes.InvalidChildCount);
    }

    [Fact]
    public void Calculate_ComputationFailure_ReturnsFailedOutcome_NeverThrows()
    {
        // decimal.MaxValue passes the negative-value validation but overflows the
        // arithmetic - the envelope must turn that into a failed outcome, mirroring
        // BaseChildSupportCalculator, instead of letting the exception escape as a 500.
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = decimal.MaxValue, AverageOvernights = 365 },
            defendant: new OregonParentInput { MonthlyIncome = decimal.MaxValue }));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Errors, e => e.Code == CalcErrorCodes.UnexpectedError);
        Assert.NotNull(outcome.RuleEffectiveDate);
    }

    [Fact]
    public void Calculate_OvernightsNotTotaling365_ReturnsError()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = 4500, AverageOvernights = 100 },
            defendant: new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 100 }));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Errors, e => e.Code == CalcErrorCodes.OvernightsMustTotal365);
    }

    [Fact]
    public void Calculate_CasOnly_IgnoresTheOvernightsRule()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = 4500 },
            defendant: new OregonParentInput { MonthlyIncome = 3200 },
            minors: 0,
            cas: 2));

        Assert.True(outcome.Success);
        // With no minor children neither parent "pays for the minors", but both owe the
        // Children Attending School their shares directly.
        Assert.Null(outcome.PaysForMinorChildren);
        Assert.True(outcome.PlaintiffTotalSupport > 0);
        Assert.True(outcome.DefendantTotalSupport > 0);
    }

    [Fact]
    public void Calculate_NegativeIncome_ReturnsNegativeInput()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = -1, AverageOvernights = 91 }));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Errors, e => e.Code == CalcErrorCodes.NegativeInput);
    }

    [Fact]
    public void Calculate_SelectionNobodyCanAfford_ReturnsCoverageSelectionUnavailable()
    {
        // Neither parent has coverage available ("none"), so selecting one of them is invalid.
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(selection: CoverageProvider.Plaintiff));

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Errors, e => e.Code == CalcErrorCodes.CoverageSelectionUnavailable);
    }

    [Fact]
    public void Calculate_AutoSelection_OnlyQualifyingParentProvides()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = 4500, AverageOvernights = 91, ChildrensHealthCoverageCost = 150 },
            defendant: new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 274 }));

        Assert.True(outcome.Success);
        Assert.Equal(CoverageProvider.Plaintiff, outcome.CoverageProvider);
    }

    [Fact]
    public void Calculate_AutoSelection_BothQualify_MoreParentingTimeWins()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input(
            plaintiff: new OregonParentInput { MonthlyIncome = 5800, AverageOvernights = 100, ChildrensHealthCoverageCost = 150 },
            defendant: new OregonParentInput { MonthlyIncome = 4600, AverageOvernights = 265, ChildrensHealthCoverageCost = 220 }));

        Assert.True(outcome.Success);
        Assert.Equal(CoverageProvider.Defendant, outcome.CoverageProvider);
    }

    [Fact]
    public void Calculate_AutoSelection_NeitherQualifies_EitherWhenAvailable()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input());

        Assert.True(outcome.Success);
        Assert.Equal(CoverageProvider.EitherWhenAvailable, outcome.CoverageProvider);
    }

    [Fact]
    public void Calculate_StampsTheRuleEffectiveDate()
    {
        OregonCalculationOutcome outcome = Calculator.Calculate(Input());

        Assert.Equal(OregonRuleParameters.Current.EffectiveDate, outcome.RuleEffectiveDate);
    }

    [Fact]
    public void OverCapChildCare_FlowsThroughUncapped_MatchingTheOfficialCalculator()
    {
        // Verified against the official DOJ Guidelines Calculator v3.6.13 (published
        // 07/01/2026) on 2026-08-28: Mother income 4500 / 91 overnights, Father income
        // 3200 / 274 overnights, 1 minor child, no coverage available to either parent,
        // cash medical included, Father pays $3,000 child care - over EVERY OAR
        // 137-050-0735 Table 1 cap (highest: $1,705). The official calculator has no
        // age/location inputs, used the full $3,000, and produced Mother total $2,360.00
        // ($2,180.00 support + $180.00 cash medical). Table 1 is guidance the filer
        // applies BEFORE entering the figure (both tools say so at the input); a future
        // "helpful" auto-cap would break this penny parity - that is what this test
        // guards. Evidence screenshots archived in the operator wiki
        // (assets/fairshare-or-childcare-caps/), run recorded on issue #172.
        OregonWorksheetInput input = new()
        {
            Plaintiff = new OregonParentInput { MonthlyIncome = 4500, AverageOvernights = 91 },
            Defendant = new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 274, ChildCareCosts = 3000 },
            JointMinorChildren = 1,
            CashMedical = CashMedicalElection.Yes,
        };

        OregonCalculationOutcome outcome = Calculator.Calculate(input);

        Assert.True(outcome.Success);
        Assert.Equal(ParentType.Plaintiff, outcome.PaysForMinorChildren);
        Assert.Equal(2360m, outcome.PlaintiffTotalSupport);
        Assert.Equal(0m, outcome.DefendantTotalSupport);
        Assert.Equal(3000m, Assert.Single(outcome.Lines, l => l.Number == "3a").Defendant);
        Assert.Equal(2180m, Assert.Single(outcome.Lines, l => l.Number == "9a").Plaintiff);
        Assert.Equal(180m, Assert.Single(outcome.Lines, l => l.Number == "9b").Plaintiff);
    }
}
