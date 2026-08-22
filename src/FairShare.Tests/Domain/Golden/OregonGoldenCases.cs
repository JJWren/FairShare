using System.IO;
using System.Reflection;
using System.Text.Json;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Tests.Domain.Golden;

/// <summary>
/// Oregon golden cases whose expected values were read back from the official DOJ Guidelines
/// Calculator workbook (see Generate-OregonGoldenCases.ps1 next to the JSON). The workbook is the
/// reference implementation; these pin the Oregon calculator to it line by line. Unrounded
/// intermediate cells carry double noise, so comparisons are made at 6 decimals - cent- and
/// dollar-rounded lines still match exactly.
/// </summary>
public static class OregonGoldenCases
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<OregonGoldenFixture> Fixture = new(() => Load("or-worksheet-golden.json"));

    public static IEnumerable<object[]> Worksheet => Fixture.Value.Cases.Select(c => new object[] { c.Name });

    public static OregonGoldenCase Get(string name) => Fixture.Value.Cases.Single(c => c.Name == name);

    private static OregonGoldenFixture Load(string fileName)
    {
        Assembly assembly = typeof(OregonGoldenCases).Assembly;
        string resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<OregonGoldenFixture>(stream, Options)
            ?? throw new InvalidOperationException($"Fixture {fileName} is empty.");
    }
}

public sealed class OregonGoldenFixture
{
    public string Source { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public List<OregonGoldenCase> Cases { get; set; } = [];
}

public sealed class OregonGoldenCase
{
    public string Name { get; set; } = string.Empty;
    public OregonGoldenInput Input { get; set; } = new();
    public string? ExpectedPaysForMinors { get; set; }
    public Dictionary<string, GoldenLine> ExpectedLines { get; set; } = [];

    public override string ToString() => Name;
}

public sealed class OregonGoldenInput
{
    public OregonGoldenParent Plaintiff { get; set; } = new();
    public OregonGoldenParent Defendant { get; set; } = new();
    public int JointMinorChildren { get; set; }
    public int JointChildrenAttendingSchool { get; set; }
    public string CashMedical { get; set; } = "No";
    public string? CoverageSelection { get; set; }
    public bool OrderCoverageAtHigherAmount { get; set; }

    public OregonWorksheetInput ToInput() => new()
    {
        Plaintiff = Plaintiff.ToParentInput(),
        Defendant = Defendant.ToParentInput(),
        JointMinorChildren = JointMinorChildren,
        JointChildrenAttendingSchool = JointChildrenAttendingSchool,
        CashMedical = Enum.Parse<CashMedicalElection>(CashMedical),
        CoverageSelection = CoverageSelection is null ? null : Enum.Parse<CoverageProvider>(CoverageSelection),
        OrderCoverageAtHigherAmount = OrderCoverageAtHigherAmount,
    };
}

public sealed class OregonGoldenParent
{
    public decimal MonthlyIncome { get; set; }
    public decimal SpousalSupportReceived { get; set; }
    public decimal SpousalSupportPaid { get; set; }
    public decimal UnionDues { get; set; }
    public decimal OwnHealthInsuranceCost { get; set; }
    public int NonJointChildren { get; set; }
    public decimal ChildCareCosts { get; set; }
    public decimal? ChildrensHealthCoverageCost { get; set; }
    public decimal AverageOvernights { get; set; }
    public decimal SocialSecurityVeteransBenefits { get; set; }
    public bool MinimumOrderException { get; set; }

    public OregonParentInput ToParentInput() => new()
    {
        MonthlyIncome = MonthlyIncome,
        SpousalSupportReceived = SpousalSupportReceived,
        SpousalSupportPaid = SpousalSupportPaid,
        UnionDues = UnionDues,
        OwnHealthInsuranceCost = OwnHealthInsuranceCost,
        NonJointChildren = NonJointChildren,
        ChildCareCosts = ChildCareCosts,
        ChildrensHealthCoverageCost = ChildrensHealthCoverageCost,
        AverageOvernights = AverageOvernights,
        SocialSecurityVeteransBenefits = SocialSecurityVeteransBenefits,
        MinimumOrderException = MinimumOrderException,
    };
}

/// <summary>Line-by-line assertion against the workbook read-back, at 6-decimal precision.</summary>
public static class OregonGoldenAssert
{
    public static void Matches(OregonGoldenCase expected, OregonCalculationOutcome actual, IReadOnlyList<string> lineNumbers)
    {
        Assert.True(actual.Success, $"{expected.Name}: expected success but got {string.Join("; ", actual.Errors.Select(e => e.Code))}");

        ParentType? expectedPayer = expected.ExpectedPaysForMinors is null
            ? null
            : Enum.Parse<ParentType>(expected.ExpectedPaysForMinors);
        Assert.Equal(expectedPayer, actual.PaysForMinorChildren);

        Assert.Equal(lineNumbers, actual.Lines.Select(l => l.Number));
        Assert.Equal(lineNumbers.Order(), expected.ExpectedLines.Keys.Order());

        foreach (string number in lineNumbers)
        {
            Assert.True(expected.ExpectedLines.TryGetValue(number, out GoldenLine? line), $"{expected.Name}: fixture has no line {number}");
            WorksheetLine actualLine = actual.Lines.Single(l => l.Number == number);
            AssertCell(expected.Name, number, "plaintiff", line.Plaintiff, actualLine.Plaintiff);
            AssertCell(expected.Name, number, "defendant", line.Defendant, actualLine.Defendant);
            AssertCell(expected.Name, number, "combined", line.Combined, actualLine.Combined);
        }
    }

    private static void AssertCell(string caseName, string number, string column, decimal? expected, decimal? actual)
    {
        decimal? rounded = actual is decimal a ? Math.Round(a, 6, MidpointRounding.AwayFromZero) : null;
        Assert.True(expected == rounded,
            $"{caseName} line {number} {column}: expected {expected?.ToString() ?? "null"}, got {rounded?.ToString() ?? "null"}");
    }
}
