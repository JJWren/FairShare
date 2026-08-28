using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using FairShare.Domain.Seeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Domain;

/// <summary>
/// Effective-dated rule vintages (#171). The synthetic vintages below are TEST DATA derived
/// from <see cref="OregonRuleParameters.Current"/> via <c>with</c> - they prove the selection
/// and coexistence mechanics and never claim to be official figures. Real prior-year values
/// enter the production <c>Vintages</c> list only from official sources (the yearly
/// schedule-refresh checklist).
/// </summary>
public class RuleVintageTests
{
    private static readonly OregonWorksheetCalculator Oregon = new();

    // Synthetic history: the real Current plus two derived vintages a year either side.
    private static readonly OregonRuleParameters OlderVintage = OregonRuleParameters.Current with
    {
        EffectiveDate = new DateOnly(2025, 7, 1),
        SelfSupportReserve = 9_999, // deliberately implausible: outcomes must visibly differ
    };

    private static readonly OregonRuleParameters NewerVintage = OregonRuleParameters.Current with
    {
        EffectiveDate = new DateOnly(2027, 7, 1),
        SelfSupportReserve = 1,
    };

    private static readonly OregonRuleParameters[] History =
        [OlderVintage, OregonRuleParameters.Current, NewerVintage];

    [Theory]
    [InlineData(2025, 6, 30, 2025)] // before the earliest -> earliest (documented backstop)
    [InlineData(2025, 7, 1, 2025)]  // exact effective date selects that vintage
    [InlineData(2026, 6, 30, 2025)] // the day before a new vintage still uses the old one
    [InlineData(2026, 7, 1, 2026)]
    [InlineData(2027, 1, 15, 2026)]
    [InlineData(2027, 7, 1, 2027)]
    [InlineData(2099, 1, 1, 2027)]  // far future -> newest known
    public void ForDate_SelectsTheVintageInForce(int year, int month, int day, int expectedVintageYear)
    {
        OregonRuleParameters selected =
            OregonRuleParameters.ForDate(new DateOnly(year, month, day), History);

        Assert.Equal(expectedVintageYear, selected.EffectiveDate.Year);
    }

    [Fact]
    public void ForDate_OverTheProductionHistory_ReturnsCurrentForToday()
    {
        // The production list carries one verified vintage today; any date at or past it -
        // and the pre-history backstop - resolves to it.
        Assert.Same(OregonRuleParameters.Current,
            OregonRuleParameters.ForDate(OregonRuleParameters.Current.EffectiveDate));
        Assert.Same(OregonRuleParameters.Current,
            OregonRuleParameters.ForDate(new DateOnly(2020, 1, 1)));
    }

    [Fact]
    public void TwoVintages_ComputeDifferentResults_AndEachNamesItsOwn()
    {
        // A payer near the self-support reserve: the synthetic older vintage's absurd SSR
        // must change the number, and each outcome names the vintage that produced it.
        OregonWorksheetInput input = new()
        {
            Plaintiff = new OregonParentInput { MonthlyIncome = 2500, AverageOvernights = 0 },
            Defendant = new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 365 },
            JointMinorChildren = 2,
        };

        OregonCalculationOutcome current = Oregon.Calculate(input, OregonRuleParameters.Current);
        OregonCalculationOutcome older = Oregon.Calculate(input, OlderVintage);

        Assert.True(current.Success);
        Assert.True(older.Success);
        Assert.Equal(OregonRuleParameters.Current.EffectiveDate, current.RuleEffectiveDate);
        Assert.Equal(OlderVintage.EffectiveDate, older.RuleEffectiveDate);
        Assert.NotEqual(current.PlaintiffTotalSupport, older.PlaintiffTotalSupport);
    }

    [Fact]
    public void AsOfDate_WithSingleVintageHistory_ResolvesCurrent()
    {
        // Honest scope: with one production vintage this cannot distinguish "AsOfDate
        // consulted" from "AsOfDate ignored" - ForDate's selection logic is proven against
        // the synthetic multi-vintage history above. This pins the single-vintage contract:
        // any pinned date on today's build computes (and names) the one verified vintage.
        OregonWorksheetInput input = new()
        {
            AsOfDate = new DateOnly(2020, 1, 1),
            Plaintiff = new OregonParentInput { MonthlyIncome = 4500, AverageOvernights = 91 },
            Defendant = new OregonParentInput { MonthlyIncome = 3200, AverageOvernights = 274 },
            JointMinorChildren = 1,
        };

        OregonCalculationOutcome outcome = Oregon.Calculate(input);

        Assert.True(outcome.Success);
        Assert.Equal(OregonRuleParameters.Current.EffectiveDate, outcome.RuleEffectiveDate);
    }

    [Fact]
    public void ForDate_UnsortedVintages_Throws()
    {
        // A silently wrong vintage on a legal figure is worse than a loud failure.
        OregonRuleParameters[] unsorted = [NewerVintage, OlderVintage];

        Assert.Throws<ArgumentException>(() =>
            OregonRuleParameters.ForDate(new DateOnly(2026, 1, 1), unsorted));
    }

    [Fact]
    public void AlabamaResults_NameTheScheduleVintage()
    {
        CS42Calculator calculator = new(NullLogger<CS42Calculator>.Instance);

        CalculationResult result = calculator.Calculate(
            new ParentData { MonthlyGrossIncome = 1200, HasPrimaryCustody = true, HealthcareCoverageCosts = 100 },
            new ParentData { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 },
            numberOfChildren: 1);

        Assert.True(result.Success);
        Assert.Equal("AL Realigned Sept 2021", result.RuleVintage);
    }

    [Fact]
    public void AlabamaErrorShells_StillNameTheScheduleVintage()
    {
        CS42Calculator calculator = new(NullLogger<CS42Calculator>.Instance);

        CalculationResult result = calculator.Calculate(new ParentData(), new ParentData(), numberOfChildren: 99);

        Assert.False(result.Success);
        Assert.Equal("AL Realigned Sept 2021", result.RuleVintage);
    }
}
