using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42CalculatorTests
{
    private static readonly string[] ExpectedLineNumbers =
        ["1", "1a", "1b", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13"];

    private readonly CS42Calculator _calculator = new(NullLogger<CS42Calculator>.Instance);

    [Fact]
    public void Calculate_PlaintiffHasPrimaryCustody_DefendantPaysExpectedAmount()
    {
        ParentData plaintiff = new()
        {
            HasPrimaryCustody = true,
            MonthlyGrossIncome = 4000,
            WorkRelatedChildcareCosts = 200,
            HealthcareCoverageCosts = 100
        };

        ParentData defendant = new()
        {
            HasPrimaryCustody = false,
            MonthlyGrossIncome = 3000
        };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal("Defendant", result.Payer);
        Assert.Equal(718, result.FinalAmount);
    }

    [Fact]
    public void Calculate_ReturnsEveryWorksheetLineInFormOrder()
    {
        CalculationResult result = _calculator.Calculate(
            new ParentData { HasPrimaryCustody = true, MonthlyGrossIncome = 4000 },
            new ParentData { MonthlyGrossIncome = 3000 },
            numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal(ExpectedLineNumbers, result.Lines.Select(l => l.Number));
        Assert.Equal(Enums.LineFormat.Percent, Line(result, "3").Format);
        Assert.All(result.Lines.Where(l => l.Number != "3"), l => Assert.Equal(Enums.LineFormat.Currency, l.Format));
    }

    // Line 11 is "Line 2 - SSR of $981": adjusted income, not gross. A parent with $1,500 gross but $600 of
    // preexisting support has only $900 adjusted - below the reserve - so line 11 is $0 and line 12 the $50 floor.
    [Fact]
    public void Calculate_SelfSupportReserve_UsesAdjustedGrossIncome()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 4000 };
        ParentData defendant = new() { MonthlyGrossIncome = 1500, PreexistingChildSupport = 600 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 1);

        Assert.True(result.Success);
        Assert.Equal(0, Line(result, "11").Defendant);
        Assert.Equal(CS42Calculator.MinimumObligation, Line(result, "12").Defendant);
        Assert.Equal("Defendant", result.Payer);
        Assert.Equal(CS42Calculator.MinimumObligation, result.FinalAmount);
    }

    // Line 12: "85% of Line 11. If less than $50, enter $50 minimum obligation."
    [Fact]
    public void Calculate_LowIncomePayer_OwesMinimumObligation()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 3000 };
        ParentData defendant = new() { MonthlyGrossIncome = 700 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal(CS42Calculator.MinimumObligation, Line(result, "12").Defendant);
        Assert.Equal(CS42Calculator.MinimumObligation, result.FinalAmount);
    }

    // Line 13 is =IF(H14=0, 0, MIN(H25, H28)): a parent with no gross income owes nothing, not the $50 minimum.
    [Fact]
    public void Calculate_ZeroGrossIncomePayer_OwesNothing()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 3000 };
        ParentData defendant = new() { MonthlyGrossIncome = 0 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal(CS42Calculator.MinimumObligation, Line(result, "12").Defendant);
        Assert.Equal(0, Line(result, "13").Defendant);
        Assert.Equal(0, result.FinalAmount);
    }

    [Fact]
    public void Calculate_DefendantHasPrimaryCustody_PlaintiffPays()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 6000 };
        ParentData defendant = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 2500 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal("Plaintiff", result.Payer);
        Assert.Equal(Line(result, "13").Plaintiff, result.FinalAmount);
    }

    // The workbook rounds the plaintiff's share (=ROUND(H17/L17,2)) and gives the defendant the remainder (=1-H18);
    // 1250/10000 = 0.125 must round UP to 0.13 the way Excel does, not to 0.12 the way banker's rounding would.
    [Fact]
    public void Calculate_PercentageShare_RoundsHalfAwayFromZeroAndSumsToOne()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 1250 };
        ParentData defendant = new() { MonthlyGrossIncome = 8750 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        WorksheetLine share = Line(result, "3");
        Assert.Equal(0.13m, share.Plaintiff);
        Assert.Equal(0.87m, share.Defendant);
        Assert.Equal(1.00m, share.Combined);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Calculate_ChildCountOutsideSchedule_ReturnsValidationError(int numberOfChildren)
    {
        CalculationResult result = _calculator.Calculate(new ParentData(), new ParentData(), numberOfChildren);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == CalcErrorCodes.InvalidChildCount);
        Assert.Empty(result.Lines);
    }

    // 30,025 rounds to the 30,050 bracket, which is off the schedule; the workbook shows #N/A there.
    [Fact]
    public void Calculate_CombinedIncomeAboveSchedule_ReturnsValidationError()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 20025 };
        ParentData defendant = new() { MonthlyGrossIncome = 10000 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.False(result.Success);
        CalcError error = Assert.Single(result.Errors);
        Assert.Equal(CalcErrorCodes.IncomeAboveSchedule, error.Code);
        Assert.Equal(Enums.ErrorSeverity.Error, error.Severity);
    }

    [Fact]
    public void Calculate_CombinedIncomeAtTopOfSchedule_Succeeds()
    {
        ParentData plaintiff = new() { HasPrimaryCustody = true, MonthlyGrossIncome = 20024 };
        ParentData defendant = new() { MonthlyGrossIncome = 10000 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
    }

    private static WorksheetLine Line(CalculationResult result, string number) => result.Lines.Single(l => l.Number == number);
}
