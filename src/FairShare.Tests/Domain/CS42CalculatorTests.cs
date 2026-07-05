using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

public class CS42CalculatorTests
{
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
    public void Calculate_ZeroChildren_ReturnsValidationError()
    {
        CalculationResult result = _calculator.Calculate(new ParentData(), new ParentData(), numberOfChildren: 0);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_CHILD_COUNT");
    }
}
