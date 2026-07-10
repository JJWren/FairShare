using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42SCalculatorTests
{
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

        // BCSO(13400, 4) = 2680; x1.5 = 4020; shares 0.32/0.68 -> 1286/2734.
        // Own share x 50% custody: plaintiff 643, defendant 1367.
        // Defendant's healthcare credit: 195 x 0.32 = 62 -> defendant owes 1305 - 643 = 662.
        Assert.True(result.Success);
        Assert.Equal("Defendant", result.Payer);
        Assert.Equal(662, result.FinalAmount);
    }

    [Fact]
    public void Calculate_IdenticalParents_NoNetTransfer()
    {
        ParentData plaintiff = new() { MonthlyGrossIncome = 5000, HealthcareCoverageCosts = 100 };
        ParentData defendant = new() { MonthlyGrossIncome = 5000, HealthcareCoverageCosts = 100 };

        CalculationResult result = _calculator.Calculate(plaintiff, defendant, numberOfChildren: 2);

        Assert.True(result.Success);
        Assert.Equal(0, result.FinalAmount);
    }

    [Fact]
    public void Calculate_ZeroChildren_ReturnsValidationError()
    {
        CalculationResult result = _calculator.Calculate(new ParentData(), new ParentData(), numberOfChildren: 0);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_CHILD_COUNT");
    }
}
