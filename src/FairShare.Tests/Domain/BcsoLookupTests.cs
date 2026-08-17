using FairShare.Domain.Helpers;
using FairShare.Domain.Seeds;

namespace FairShare.Tests.Domain;

public class BcsoLookupTests
{
    [Theory]
    [InlineData(250, 1, 52)]
    [InlineData(250, 6, 128)]
    [InlineData(5000, 2, 1194)]
    [InlineData(10000, 4, 2216)]
    [InlineData(30000, 1, 2456)]
    [InlineData(30000, 6, 5586)]
    public void Get_ExactBracket_ReturnsScheduleAmount(int cagi, int children, int expected)
    {
        Assert.Equal(expected, BcsoLookup.Get(cagi, children));
    }

    // Excel: VLOOKUP(MAX(250, ROUND(CAGI/50,0)*50), ...) - halves round up, so 2225 lands on 2250, 2224 on 2200.
    [Theory]
    [InlineData(2224, 2200)]
    [InlineData(2225, 2250)]
    [InlineData(2249, 2250)]
    [InlineData(2274, 2250)]
    [InlineData(2275, 2300)]
    [InlineData(13417, 13400)]
    [InlineData(30024, 30000)]
    public void ScheduleKey_RoundsToNearestFiftyWithHalvesUp(int cagi, int expectedKey)
    {
        Assert.Equal(expectedKey, BcsoLookup.ScheduleKey(cagi));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(249)]
    [InlineData(-500)]
    public void ScheduleKey_BelowFloor_UsesFloorRow(int cagi)
    {
        Assert.Equal(BcsoLookup.ScheduleFloor, BcsoLookup.ScheduleKey(cagi));
        Assert.Equal(BcsoLookup.Get(BcsoLookup.ScheduleFloor, 3), BcsoLookup.Get(cagi, 3));
    }

    [Fact]
    public void Get_JustBelowCeilingRoundsDown_Succeeds()
    {
        Assert.Equal(BcsoLookup.Get(30000, 2), BcsoLookup.Get(30024, 2));
    }

    [Fact]
    public void Get_RoundsAboveCeiling_ThrowsIncomeAboveSchedule()
    {
        IncomeAboveScheduleException ex = Assert.Throws<IncomeAboveScheduleException>(() => BcsoLookup.Get(30025, 2));
        Assert.Equal(30025, ex.CombinedAdjustedGrossIncome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Get_ChildrenOutOfRange_Throws(int children)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BcsoLookup.Get(5000, children));
    }

    // The seed carries $0-$200 rows for completeness; the workbook never reaches them (it floors at 250),
    // and they must never disagree with the floor row they stand in for.
    [Fact]
    public void Table_RowsBelowFloor_EqualFloorRow()
    {
        for (int cagi = 0; cagi < BcsoLookup.ScheduleFloor; cagi += BcsoLookup.Increment)
        {
            for (int children = BcsoLookup.MinChildren; children <= BcsoLookup.MaxChildren; children++)
            {
                Assert.Equal(BcsoLookup.Table[(BcsoLookup.ScheduleFloor, children)], BcsoLookup.Table[(cagi, children)]);
            }
        }
    }

    [Fact]
    public void Table_CoversEveryBracketFromZeroToCeiling()
    {
        int brackets = BcsoLookup.ScheduleCeiling / BcsoLookup.Increment + 1;
        Assert.Equal(brackets * BcsoLookup.MaxChildren, BcsoLookup.Table.Count);
    }
}
