using System.IO;
using ClosedXML.Excel;
using FairShare.Api.Services.Export;
using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Tests.Domain.Golden;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Export;

/// <summary>
/// The embedded official workbook is the oracle: fill it with a golden case's inputs, let ClosedXML evaluate the
/// AOC's own formulas, and check every mapped cell against the calculator's worksheet lines. This catches drift in
/// either direction - a calculator that stops matching the sheet, or a template map pointing at the wrong cell.
/// </summary>
public class WorkbookOracleTests
{
    private readonly ClosedXmlWorksheetExporter _exporter = new(TimeProvider.System);

    [Theory]
    [MemberData(nameof(GoldenCases.CS42), MemberType = typeof(GoldenCases))]
    public void CS42_WorkbookAgreesWithCalculatorOnEveryLine(string caseName)
    {
        AssertWorkbookMatchesCalculator(
            WorksheetTemplates.AlabamaCS42,
            new CS42Calculator(NullLogger<CS42Calculator>.Instance),
            GoldenCases.GetCS42(caseName));
    }

    [Theory]
    [MemberData(nameof(GoldenCases.CS42S), MemberType = typeof(GoldenCases))]
    public void CS42S_WorkbookAgreesWithCalculatorOnEveryLine(string caseName)
    {
        AssertWorkbookMatchesCalculator(
            WorksheetTemplates.AlabamaCS42S,
            new CS42SCalculator(NullLogger<CS42SCalculator>.Instance),
            GoldenCases.GetCS42S(caseName));
    }

    // Above the schedule the VLOOKUP has nothing to find - the sheet shows #N/A (and, saved, would keep the
    // template's stale sample value), which is why the API refuses to export a calculation that did not succeed.
    [Fact]
    public void CS42_IncomeAboveSchedule_WorkbookShowsError()
    {
        WorksheetExportInput input = new("AL", "CS42", 2,
            new() { MonthlyGrossIncome = 20025 }, new() { MonthlyGrossIncome = 10000 }, null, null);

        using XLWorkbook workbook = Open(_exporter.Export(input));
        workbook.RecalculateAllFormulas();
        IXLCell basicObligation = workbook.Worksheet(WorksheetTemplates.AlabamaCS42.SheetName).Cell("L19");

        Assert.True(basicObligation.Value.IsError, $"expected an error value, got {basicObligation.Value}");
    }

    [Fact]
    public void EveryRegisteredTemplate_IsEmbeddedInTheApiAssembly()
    {
        string[] resources = typeof(ClosedXmlWorksheetExporter).Assembly.GetManifestResourceNames();

        foreach (WorksheetTemplate template in WorksheetTemplates.All)
        {
            Assert.Contains(template.ResourceName, resources);
        }
    }

    private void AssertWorkbookMatchesCalculator(WorksheetTemplate template, IChildSupportCalculator calculator, GoldenCase golden)
    {
        WorksheetExportInput input = new(template.State, template.Form, golden.NumberOfChildren,
            golden.Plaintiff.ToParentData(), golden.Defendant.ToParentData(), null, null);

        CalculationResult result = calculator.Calculate(input.Plaintiff, input.Defendant, input.NumberOfChildren);
        Assert.True(result.Success, $"{golden.Name}: calculator did not succeed");

        using XLWorkbook workbook = Open(_exporter.Export(input));
        workbook.RecalculateAllFormulas();
        IXLWorksheet sheet = workbook.Worksheet(template.SheetName);

        foreach (WorksheetLine line in result.Lines)
        {
            LineCells cells = template.Lines[line.Number];
            bool percent = line.Format == Enums.LineFormat.Percent;

            AssertCell(golden.Name, line.Number, "plaintiff", sheet, cells.Plaintiff, line.Plaintiff, percent);
            AssertCell(golden.Name, line.Number, "defendant", sheet, cells.Defendant, line.Defendant, percent);
            AssertCell(golden.Name, line.Number, "combined", sheet, cells.Combined, line.Combined, percent);
        }
    }

    private static void AssertCell(string caseName, string number, string column, IXLWorksheet sheet, string? address, decimal? expected, bool percent)
    {
        if (address is null)
        {
            Assert.True(expected is null, $"{caseName} line {number} {column}: calculator has a value but the form has no cell");
            return;
        }

        XLCellValue value = sheet.Cell(address).Value;

        // CS-42-S line 14 leaves the losing column as "" (=IF(..., "")); the calculator models that as null.
        if (value.IsBlank || (value.IsText && value.GetText().Length == 0))
        {
            Assert.True(expected is null, $"{caseName} line {number} {column} ({address}): sheet is blank, calculator has {expected}");
            return;
        }

        Assert.True(value.IsNumber, $"{caseName} line {number} {column} ({address}): sheet has {value} (not a number)");
        decimal actual = percent
            ? Math.Round((decimal)value.GetNumber(), 2, MidpointRounding.AwayFromZero)
            : (decimal)Math.Round(value.GetNumber(), 0, MidpointRounding.AwayFromZero);

        Assert.True(expected == actual, $"{caseName} line {number} {column} ({address}): sheet {actual}, calculator {expected?.ToString() ?? "null"}");
    }

    private static XLWorkbook Open(WorksheetExport export) => new(new MemoryStream(export.Content));
}
