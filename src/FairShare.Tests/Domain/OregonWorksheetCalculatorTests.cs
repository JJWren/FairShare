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
}
