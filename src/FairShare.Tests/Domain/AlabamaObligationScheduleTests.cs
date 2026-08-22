using FairShare.Domain.Helpers;
using FairShare.Domain.Seeds;

namespace FairShare.Tests.Domain;

public class AlabamaObligationScheduleTests
{
    [Fact]
    public void Bounds_MatchTheAlabamaSchedule()
    {
        Assert.Equal(BcsoLookup.MinChildren, AlabamaObligationSchedule.Instance.MinChildren);
        Assert.Equal(BcsoLookup.MaxChildren, AlabamaObligationSchedule.Instance.MaxChildren);
    }

    [Theory]
    [InlineData(250, 1)]
    [InlineData(5000, 3)]
    [InlineData(30000, 6)]
    public void GetBasicObligation_DelegatesToBcsoLookup(int cagi, int children)
    {
        Assert.Equal(BcsoLookup.Get(cagi, children), AlabamaObligationSchedule.Instance.GetBasicObligation(cagi, children));
    }

    [Fact]
    public void GetBasicObligation_AboveCeiling_ThrowsWithAlabamaWording()
    {
        IncomeAboveScheduleException ex = Assert.Throws<IncomeAboveScheduleException>(
            () => AlabamaObligationSchedule.Instance.GetBasicObligation(30025, 2));

        Assert.Equal(30025, ex.CombinedAdjustedGrossIncome);
        Assert.Contains("Alabama schedule", ex.Message);
    }
}
