using FairShare.Domain.Models;

namespace FairShare.Api.Services.Export;

/// <summary>
/// Fills a state's official worksheet workbook with a calculation's inputs and hands back the file.
/// Only input cells are written - the workbook's own formulas stay live so the sheet proves its own numbers.
/// </summary>
public interface IWorksheetExporter
{
    /// <summary>Whether an official workbook template is registered for this state/form.</summary>
    bool CanExport(string state, string form);

    /// <summary>Produces the filled workbook. Throws <see cref="KeyNotFoundException"/> when <see cref="CanExport"/> is false.</summary>
    WorksheetExport Export(WorksheetExportInput input);
}

/// <summary>
/// Everything the exporter writes into the workbook: the same figures the calculator receives, plus the optional
/// display names for the two name lines at the top of the form.
/// </summary>
public sealed record WorksheetExportInput(
    string State,
    string Form,
    int NumberOfChildren,
    ParentData Plaintiff,
    ParentData Defendant,
    string? PlaintiffName,
    string? DefendantName);

/// <summary>The exported file: bytes, suggested filename and MIME type.</summary>
public sealed record WorksheetExport(byte[] Content, string FileName, string ContentType);
