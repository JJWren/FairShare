using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using FairShare.Api.Services.Export;
using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using FairShare.Tests.Domain.Golden;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Tests.Export;

/// <summary>
/// The embedded Oregon workbook (Templates/OR/Worksheet.xlsx, the official DOJ Guidelines
/// Calculator converted from .xls) is the oracle: fill it with each golden case's inputs, let
/// ClosedXML evaluate the state's own formulas, and check every worksheet line against the
/// calculator. Same drift guard as the Alabama oracle, without needing desktop Excel. The cell
/// maps mirror Generate-OregonGoldenCases.ps1 next to the golden fixture.
/// </summary>
public class OregonWorkbookOracleTests
{
    private const string ResourceName = "FairShare.Api.Templates.OR.Worksheet.xlsx";
    private const string SheetName = "child support worksheet";

    /// <summary>Worksheet line id -> plaintiff / defendant / combined cells (null = no cell on the form).</summary>
    private static readonly IReadOnlyDictionary<string, LineCells> Lines = new Dictionary<string, LineCells>
    {
        ["1a"] = new("D12", "E12", null),
        ["1b"] = new("D18", "E18", null),
        ["1c"] = new("D19", "E19", null),
        ["1d"] = new(null, null, "D20"),
        ["1e"] = new(null, null, "D22"),
        ["1f"] = new("D23", "E23", null),
        ["1g"] = new("D24", "E24", null),
        ["1h"] = new("D25", "E25", "F26"),
        ["1i"] = new("D27", "E27", null),
        ["1j"] = new("D28", "E28", null),
        ["2a"] = new(null, null, "F32"),
        ["2b"] = new("D34", "E34", null),
        ["3a"] = new("D38", "E38", null),
        ["3b"] = new("D39", "E39", null),
        ["3c"] = new("D40", "E40", null),
        ["3d"] = new("D42", "E42", null),
        ["4a"] = new("D46", "E46", null),
        ["4b"] = new("D47", "E47", null),
        ["4c"] = new("D48", "E48", "F49"),
        ["4f"] = new(null, null, "F53"),
        ["4g"] = new("D54", "E54", null),
        ["4h"] = new("D55", "E55", null),
        ["4i"] = new("D56", "E56", null),
        ["5b"] = new("D62", "E62", null),
        ["6a"] = new("D66", "E66", null),
        ["6b"] = new("D67", "E67", null),
        ["6c"] = new("D68", "E68", null),
        ["6d"] = new("D69", "E69", null),
        ["6e"] = new("D70", "E70", null),
        ["6f"] = new("D71", "E71", null),
        ["7a"] = new("D75", "E75", null),
        ["7b"] = new("D76", "E76", null),
        ["8a"] = new("D81", "E81", null),
        ["8c"] = new("D83", "E83", null),
        ["8d"] = new("D84", "E84", null),
        ["8e"] = new("D85", "E85", null),
        ["8f"] = new("D86", "E86", null),
        ["8g"] = new("D87", "E87", null),
        ["8h"] = new("D88", "E88", null),
        ["9a"] = new("D92", "E92", null),
        ["9b"] = new("D93", "E93", null),
        ["9c"] = new("D94", "E94", null),
        ["9d"] = new("D95", "E95", null),
        ["9e"] = new("D96", "E96", null),
        ["9g"] = new(null, null, "D98"),
    };

    [Fact]
    public void OregonTemplate_IsEmbeddedInTheApiAssembly()
    {
        string[] resources = typeof(ClosedXmlWorksheetExporter).Assembly.GetManifestResourceNames();

        Assert.Contains(ResourceName, resources);
    }

    [Theory]
    [MemberData(nameof(OregonGoldenCases.Worksheet), MemberType = typeof(OregonGoldenCases))]
    public void Workbook_AgreesWithCalculatorOnEveryLine(string caseName)
    {
        OregonGoldenCase golden = OregonGoldenCases.Get(caseName);
        OregonWorksheetInput input = golden.Input.ToInput();

        OregonCalculationOutcome outcome = new OregonWorksheetCalculator().Calculate(input);
        Assert.True(outcome.Success, $"{golden.Name}: calculator did not succeed");

        using XLWorkbook workbook = OpenTemplate();
        IXLWorksheet sheet = workbook.Worksheet(SheetName);
        WriteInputs(sheet, golden.Input);
        workbook.RecalculateAllFormulas();

        foreach (WorksheetLine line in outcome.Lines)
        {
            Assert.True(Lines.TryGetValue(line.Number, out LineCells? cells),
                $"{golden.Name}: calculator produced line {line.Number} but the cell map has no entry for it");
            AssertCell(golden.Name, line.Number, "plaintiff", sheet, cells!.Plaintiff, line.Plaintiff);
            AssertCell(golden.Name, line.Number, "defendant", sheet, cells.Defendant, line.Defendant);
            AssertCell(golden.Name, line.Number, "combined", sheet, cells.Combined, line.Combined);
        }
    }

    private static void WriteInputs(IXLWorksheet sheet, OregonGoldenInput input)
    {
        sheet.Cell("D10").Value = "Plaintiff";
        sheet.Cell("E10").Value = "Defendant";

        WriteParent(sheet, "D", input.Plaintiff);
        WriteParent(sheet, "E", input.Defendant);

        sheet.Cell("D20").Value = input.JointMinorChildren;
        sheet.Cell("D22").Value = input.JointChildrenAttendingSchool;

        sheet.Cell("D51").Value = input.OrderCoverageAtHigherAmount ? "Yes" : "No";
        sheet.Cell("D60").Value = input.CashMedical switch
        {
            "Yes" => "y",
            "Contingent" => "c",
            _ => "n",
        };

        // Line 4f: the golden cases carry an explicit, line-4d-legal selection.
        sheet.Cell("D52").Value = input.CoverageSelection switch
        {
            "Plaintiff" => "Plaintiff",
            "Defendant" => "Defendant",
            "Both" => "Plaintiff and Defendant",
            _ => "Either parent when available",
        };
    }

    private static void WriteParent(IXLWorksheet sheet, string column, OregonGoldenParent parent)
    {
        sheet.Cell($"{column}12").Value = parent.MonthlyIncome;
        sheet.Cell($"{column}14").Value = parent.SpousalSupportReceived;
        sheet.Cell($"{column}15").Value = parent.SpousalSupportPaid;
        sheet.Cell($"{column}16").Value = parent.UnionDues;
        sheet.Cell($"{column}17").Value = parent.OwnHealthInsuranceCost;
        sheet.Cell($"{column}19").Value = parent.NonJointChildren;
        sheet.Cell($"{column}38").Value = parent.ChildCareCosts;

        if (parent.ChildrensHealthCoverageCost is decimal premium)
        {
            sheet.Cell($"{column}46").Value = premium;
        }
        else
        {
            sheet.Cell($"{column}46").Value = "none";
        }

        sheet.Cell($"{column}66").Value = parent.AverageOvernights;
        sheet.Cell($"{column}85").Value = parent.SocialSecurityVeteransBenefits;
        sheet.Cell($"{column}82").Value = parent.MinimumOrderException ? "Yes" : "No";
    }

    private static void AssertCell(string caseName, string number, string column, IXLWorksheet sheet, string? address, decimal? expected)
    {
        if (address is null)
        {
            Assert.True(expected is null, $"{caseName} line {number} {column}: calculator has a value but the form has no cell");
            return;
        }

        XLCellValue value = sheet.Cell(address).Value;

        // Line 4a shows the literal "none" when a parent has no coverage; the calculator models it as null.
        if (value.IsBlank || (value.IsText && (value.GetText().Length == 0 || value.GetText() == "none")))
        {
            Assert.True(expected is null, $"{caseName} line {number} {column} ({address}): sheet is blank/none, calculator has {expected}");
            return;
        }

        Assert.True(value.IsNumber, $"{caseName} line {number} {column} ({address}): sheet has {value} (not a number)");

        // Unrounded intermediate cells carry double noise; 6 decimals keeps every cent- and
        // dollar-rounded line exact (same tolerance as the golden fixture).
        decimal sheetValue = Math.Round((decimal)value.GetNumber(), 6, MidpointRounding.AwayFromZero);
        decimal? calcValue = expected is decimal e ? Math.Round(e, 6, MidpointRounding.AwayFromZero) : null;

        Assert.True(calcValue == sheetValue, $"{caseName} line {number} {column} ({address}): sheet {sheetValue}, calculator {calcValue?.ToString() ?? "null"}");
    }

    private static XLWorkbook OpenTemplate()
    {
        Assembly api = typeof(ClosedXmlWorksheetExporter).Assembly;
        using Stream stream = api.GetManifestResourceStream(ResourceName)!;
        MemoryStream buffer = new();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return new XLWorkbook(buffer);
    }
}
