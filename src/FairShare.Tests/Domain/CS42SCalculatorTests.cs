using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42SCalculatorTests
{
    private static readonly string[] ExpectedLineNumbers =
        ["1", "1a", "1b", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14"];

    private readonly CS42SCalculator _calculator = new(NullLogger<CS42SCalculator>.Instance);

    // Regression for the transposed-obligation bug: each parent's obligation was computed
    // from the OTHER parent's income share, so the lower earner always came out as the
    // payer (this exact scenario reported Plaintiff owing $786).
    [Fact]
    public void Calculate_HigherEarningDefendant_DefendantPays()
    {
        ParentData plaintiff = new()
        {
            MonthlyGrossIncome = 4244
        };

        ParentData defendant = new()
        {
            MonthlyGrossIncome = 9173,
            HealthcareCoverageCosts = 195
        };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 4);

        // Official workbook: BCSO(13400, 4) = 2680; line 5 = 4020; shares 0.32/0.68; line 9 = 4215;
        // line 10 = 1349/2866; line 12 = 2010 each; line 13 = 1349-0-2010 = -661 / 2866-195-2010 = 661.
        // (An earlier rearranged formula produced 662 - one dollar off the state's own sheet.)
        Assert.True(result.Success);
        Assert.Equal("Defendant", result.Payer);
        Assert.Equal(661, result.FinalAmount);
    }

    [Fact]
    public void Calculate_IdenticalParents_NoNetTransfer()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 5000, HealthcareCoverageCosts = 100 };
        ParentData defendant = new() { MonthlyGrossIncome = 5000, HealthcareCoverageCosts = 100 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal(0, result.FinalAmount);
        // The UI renders "No net transfer." only when the payer is blank - a named payer
        // with a $0 amount would display as a misleading "X owes $0".
        Assert.True(string.IsNullOrWhiteSpace(result.Payer));
        // The worksheet itself still shows the tie in the defendant's column (=IF(J30>=H30, J30, "")).
        WorksheetLine order = Line(result, "14");
        Assert.Null(order.Plaintiff);
        Assert.Equal(0, order.Defendant);
    }

    [Fact]
    public void Calculate_ReturnsEveryWorksheetLineInFormOrder()
    {
        CalculationResult result = _calculator.Calculate(
            new ParentData { MonthlyGrossIncome = 4244 },
            new ParentData { MonthlyGrossIncome = 9173, HealthcareCoverageCosts = 195 },
            numberOfChildren: 4);

        Assert.True(result.Success);
        Assert.Equal(ExpectedLineNumbers, result.Lines.Select(l => l.Number));
        Assert.Equal(Enums.LineFormat.Percent, Line(result, "3").Format);
    }

    // Line 14 places the higher line-13 amount in that parent's column and leaves the other blank.
    [Fact]
    public void Calculate_HigherEarningPlaintiff_PlaintiffColumnCarriesTheOrder()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 7000, WorkRelatedChildcareCosts = 300 };
        ParentData defendant = new() { MonthlyGrossIncome = 3000, HealthcareCoverageCosts = 100 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal("Plaintiff", result.Payer);
        WorksheetLine order = Line(result, "14");
        Assert.Equal(result.FinalAmount, order.Plaintiff);
        Assert.Null(order.Defendant);
        Assert.Equal(Line(result, "13").Plaintiff, order.Plaintiff);
    }

    // CS-42-S rounds the DEFENDANT's share (=ROUND(J17/L17,2)) and gives the plaintiff the remainder (=1-J19).
    // 11880/14400 = 0.825 rounds UP to 0.83 in Excel; banker's rounding would say 0.82 and swing line 10 by $50.
    [Fact]
    public void Calculate_PercentageShare_RoundsDefendantHalfAwayFromZero()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 2520 };
        ParentData defendant = new() { MonthlyGrossIncome = 12221, PreexistingChildSupport = 341 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 6);

        WorksheetLine share = Line(result, "3");
        Assert.Equal(0.17m, share.Plaintiff);
        Assert.Equal(0.83m, share.Defendant);
    }

    // The form assumes 50/50 physical custody, so the primary-custody flag must not change anything.
    [Fact]
    public void Calculate_IgnoresPrimaryCustodyFlag()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 4244 };
        ParentData defendant = new() { MonthlyGrossIncome = 9173, HealthcareCoverageCosts = 195 };

        CalculationResult without = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 4);
        plaintiff.HasPrimaryCustody = true;
        CalculationResult with = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 4);

        Assert.Equal(without.Payer, with.Payer);
        Assert.Equal(without.FinalAmount, with.FinalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Calculate_ChildCountOutsideSchedule_ReturnsValidationError(int numberOfChildren)
    {
        CalculationResult result = _calculator.Calculate(new ParentData(), new ParentData(), numberOfChildren);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == CalcErrorCodes.InvalidChildCount);
    }

    [Fact]
    public void Calculate_CombinedIncomeAboveSchedule_ReturnsValidationError()
    {
        CalculationResult result = _calculator.Calculate(
            new ParentData { MonthlyGrossIncome = 20025 },
            new ParentData { MonthlyGrossIncome = 10000 },
            numberOfChildren: 2);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == CalcErrorCodes.IncomeAboveSchedule);
    }

    private static WorksheetLine Line(CalculationResult result, string number) => result.Lines.Single(l => l.Number == number);
}
