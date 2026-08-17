using System.Collections.Concurrent;
using System.Reflection;
using ClosedXML.Excel;
using FairShare.Domain.Models;

namespace FairShare.Api.Services.Export;

/// <summary>
/// Fills the embedded official AOC workbook with ClosedXML. The template's formulas are never touched: the
/// exporter writes the number of children, each parent's five input cells and the two optional names, then
/// recalculates so cached values are present for viewers that don't recalculate on open, and asks Excel to
/// recalculate on load anyway.
/// </summary>
public sealed class ClosedXmlWorksheetExporter(TimeProvider timeProvider) : IWorksheetExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, byte[]> _templateBytes = new();

    public bool CanExport(string state, string form) => WorksheetTemplates.Find(state, form) is not null;

    public WorksheetExport Export(WorksheetExportInput input)
    {
        WorksheetTemplate template = WorksheetTemplates.Find(input.State, input.Form)
            ?? throw new KeyNotFoundException($"No worksheet template registered for {input.State}/{input.Form}.");

        using MemoryStream source = new(_templateBytes.GetOrAdd(template.ResourceName, LoadResource));
        using XLWorkbook workbook = new(source);
        IXLWorksheet sheet = workbook.Worksheet(template.SheetName);

        FillInputs(sheet, template, input);

        // Belt and braces: evaluate on save so cached values exist for viewers that trust them (LibreOffice,
        // previewers), and ask Excel for a full recalc on open regardless. The sheet stays protected exactly
        // as the AOC published it.
        workbook.CalculateMode = XLCalculateMode.Auto;
        workbook.FullCalculationOnLoad = true;

        using MemoryStream output = new();
        workbook.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = true });

        string fileName = $"FairShare_{template.State.ToUpperInvariant()}_{template.FileStem}_{_timeProvider.GetUtcNow():yyyyMMdd}.xlsx";
        return new WorksheetExport(output.ToArray(), fileName, ContentType);
    }

    /// <summary>
    /// Writes only the cells a person would type into: the input cells the AOC left unlocked, plus the name lines.
    /// </summary>
    public static void FillInputs(IXLWorksheet sheet, WorksheetTemplate template, WorksheetExportInput input)
    {
        sheet.Cell(template.ChildrenCell).Value = input.NumberOfChildren;
        WriteParent(sheet, template.Plaintiff, input.Plaintiff);
        WriteParent(sheet, template.Defendant, input.Defendant);

        if (!string.IsNullOrWhiteSpace(input.PlaintiffName))
        {
            sheet.Cell(template.PlaintiffNameCell).Value = input.PlaintiffName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(input.DefendantName))
        {
            sheet.Cell(template.DefendantNameCell).Value = input.DefendantName.Trim();
        }
    }

    private static void WriteParent(IXLWorksheet sheet, ParentCells cells, ParentData parent)
    {
        sheet.Cell(cells.Gross).Value = parent.MonthlyGrossIncome;
        sheet.Cell(cells.ChildSupport).Value = parent.PreexistingChildSupport;
        sheet.Cell(cells.Alimony).Value = parent.PreexistingAlimony;
        sheet.Cell(cells.Childcare).Value = parent.WorkRelatedChildcareCosts;
        sheet.Cell(cells.Healthcare).Value = parent.HealthcareCoverageCosts;
    }

    private static byte[] LoadResource(string resourceName)
    {
        Assembly assembly = typeof(ClosedXmlWorksheetExporter).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded worksheet template '{resourceName}' is missing. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
