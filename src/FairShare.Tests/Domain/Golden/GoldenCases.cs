using System.IO;
using System.Reflection;
using System.Text.Json;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;

namespace FairShare.Tests.Domain.Golden;

/// <summary>
/// Golden cases whose expected values were read back from the official Alabama AOC workbooks
/// (see Generate-GoldenCases.ps1 next to the JSON files). The workbook is the reference implementation;
/// these tests pin the calculators to it line by line.
/// </summary>
public static class GoldenCases
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<GoldenFixture> Cs42Fixture = new(() => Load("al-cs42-golden.json"));
    private static readonly Lazy<GoldenFixture> Cs42SFixture = new(() => Load("al-cs42s-golden.json"));

    // MemberData yields the case name only so each case shows up as its own test; the test body
    // resolves the full case through Get(...) below.
    public static IEnumerable<object[]> CS42 => Cs42Fixture.Value.Cases.Select(c => new object[] { c.Name });
    public static IEnumerable<object[]> CS42S => Cs42SFixture.Value.Cases.Select(c => new object[] { c.Name });

    public static GoldenCase GetCS42(string name) => Find(Cs42Fixture.Value, name);
    public static GoldenCase GetCS42S(string name) => Find(Cs42SFixture.Value, name);

    private static GoldenCase Find(GoldenFixture fixture, string name)
        => fixture.Cases.Single(c => c.Name == name);

    private static GoldenFixture Load(string fileName)
    {
        Assembly assembly = typeof(GoldenCases).Assembly;
        string resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<GoldenFixture>(stream, Options)
            ?? throw new InvalidOperationException($"Fixture {fileName} is empty.");
    }
}

public sealed class GoldenFixture
{
    public string Source { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public List<GoldenCase> Cases { get; set; } = [];
}

public sealed class GoldenCase
{
    public string Name { get; set; } = string.Empty;
    public int NumberOfChildren { get; set; }
    public GoldenParent Plaintiff { get; set; } = new();
    public GoldenParent Defendant { get; set; } = new();
    public string ExpectedPayer { get; set; } = string.Empty;
    public int ExpectedAmount { get; set; }
    public Dictionary<string, GoldenLine> ExpectedLines { get; set; } = [];

    public override string ToString() => Name;
}

public sealed class GoldenParent
{
    public bool HasPrimaryCustody { get; set; }
    public int MonthlyGrossIncome { get; set; }
    public int PreexistingChildSupport { get; set; }
    public int PreexistingAlimony { get; set; }
    public int WorkRelatedChildcareCosts { get; set; }
    public int HealthcareCoverageCosts { get; set; }

    public ParentData ToParentData() => new()
    {
        HasPrimaryCustody = HasPrimaryCustody,
        MonthlyGrossIncome = MonthlyGrossIncome,
        PreexistingChildSupport = PreexistingChildSupport,
        PreexistingAlimony = PreexistingAlimony,
        WorkRelatedChildcareCosts = WorkRelatedChildcareCosts,
        HealthcareCoverageCosts = HealthcareCoverageCosts
    };
}

public sealed class GoldenLine
{
    public decimal? Plaintiff { get; set; }
    public decimal? Defendant { get; set; }
    public decimal? Combined { get; set; }
}

/// <summary>
/// Shared assertions for the golden theories of both forms.
/// </summary>
public static class GoldenAssert
{
    public static void Matches(GoldenCase expected, CalculationResult actual)
    {
        Assert.True(actual.Success, $"{expected.Name}: expected success but got {string.Join("; ", actual.Errors.Select(e => e.Code))}");
        Assert.Equal(expected.ExpectedPayer, actual.Payer);
        Assert.Equal(expected.ExpectedAmount, actual.FinalAmount);

        // Same lines, same order, as the paper form.
        Assert.Equal(expected.ExpectedLines.Keys, actual.Lines.Select(l => l.Number));

        foreach ((string number, GoldenLine line) in expected.ExpectedLines)
        {
            WorksheetLine actualLine = actual.Lines.Single(l => l.Number == number);
            Assert.True(line.Plaintiff == actualLine.Plaintiff, $"{expected.Name} line {number} plaintiff: expected {line.Plaintiff?.ToString() ?? "null"}, got {actualLine.Plaintiff?.ToString() ?? "null"}");
            Assert.True(line.Defendant == actualLine.Defendant, $"{expected.Name} line {number} defendant: expected {line.Defendant?.ToString() ?? "null"}, got {actualLine.Defendant?.ToString() ?? "null"}");
            Assert.True(line.Combined == actualLine.Combined, $"{expected.Name} line {number} combined: expected {line.Combined?.ToString() ?? "null"}, got {actualLine.Combined?.ToString() ?? "null"}");
        }
    }
}
