using FairShare.Domain.Seeds;

namespace FairShare.Tests.Domain;

public class OregonScaleLookupTests
{
    // Spot values read straight off the "scale table" sheet of the official DOJ workbook.
    [Theory]
    [InlineData(0, 1, 50)]
    [InlineData(1000, 10, 50)]
    [InlineData(1001, 1, 65)]
    [InlineData(1001, 10, 71)]
    [InlineData(1051, 1, 98)]
    [InlineData(1051, 10, 108)]
    [InlineData(1151, 5, 172)]
    [InlineData(29951, 1, 1987)]
    [InlineData(29951, 2, 2801)]
    [InlineData(29951, 10, 5578)]
    public void Get_ReturnsTheWorkbookValue(int income, int children, int expected)
    {
        Assert.Equal(expected, OregonScaleLookup.Get(income, children));
    }

    [Fact]
    public void Get_BetweenRows_UsesTheLowerBracket()
    {
        // $1,050 sits inside the 1001-1050 bracket; Oregon never rounds up to the 1051 row.
        Assert.Equal(OregonScaleLookup.Get(1001, 3), OregonScaleLookup.Get(1050, 3));
        Assert.Equal(OregonScaleLookup.Get(1151, 5), OregonScaleLookup.Get(1200, 5));
    }

    [Theory]
    [InlineData(30000)]
    [InlineData(45000)]
    [InlineData(1000000)]
    public void Get_AboveTheScale_CapsAtTheTopRow(int income)
    {
        Assert.Equal(OregonScaleLookup.Get(OregonScaleLookup.TopThreshold, 4), OregonScaleLookup.Get(income, 4));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5000)]
    [InlineData(500)]
    public void Get_BelowTheFirstDataRow_UsesTheFlatFiftyRow(int income)
    {
        for (int children = OregonScaleLookup.MinChildren; children <= OregonScaleLookup.MaxChildren; children++)
        {
            Assert.Equal(50, OregonScaleLookup.Get(income, children));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Get_ChildrenOutOfRange_Throws(int children)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OregonScaleLookup.Get(5000, children));
    }

    [Fact]
    public void Table_HasEveryRowAndColumn()
    {
        // $0 plus $1,001 through $29,951 in $50 steps, ten child columns each.
        int rows = 1 + (OregonScaleLookup.TopThreshold - 1001) / 50 + 1;
        Assert.Equal(rows * OregonScaleLookup.MaxChildren, OregonScaleLookup.Table.Count);
    }
}
