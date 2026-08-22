using FairShare.Domain.Seeds;

namespace FairShare.Tests.Domain;

public class OregonObligationScheduleTests
{
    [Fact]
    public void Bounds_AcceptAnyFamilySize()
    {
        // The scale has ten columns, but the rule prices larger families with the ten-child
        // figure - so the schedule itself imposes no upper bound on the child count.
        Assert.Equal(OregonScaleLookup.MinChildren, OregonObligationSchedule.Instance.MinChildren);
        Assert.Equal(int.MaxValue, OregonObligationSchedule.Instance.MaxChildren);
    }

    [Fact]
    public void GetBasicObligation_MoreThanTenChildren_UsesTheTenChildColumn()
    {
        Assert.Equal(
            OregonObligationSchedule.Instance.GetBasicObligation(5000, 10),
            OregonObligationSchedule.Instance.GetBasicObligation(5000, 13));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, 3)]
    [InlineData(29951, 10)]
    public void GetBasicObligation_DelegatesToOregonScaleLookup(int income, int children)
    {
        Assert.Equal(OregonScaleLookup.Get(income, children), OregonObligationSchedule.Instance.GetBasicObligation(income, children));
    }

    [Fact]
    public void GetBasicObligation_AboveTheScale_CapsInsteadOfThrowing()
    {
        // Oregon caps at the $30,000 row (rebuttably); only Alabama's schedule errors above its top.
        Assert.Equal(
            OregonObligationSchedule.Instance.GetBasicObligation(OregonScaleLookup.TopThreshold, 2),
            OregonObligationSchedule.Instance.GetBasicObligation(100000, 2));
    }
}
