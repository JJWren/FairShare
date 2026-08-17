namespace FairShare.Api.Services.Export;

/// <summary>The five input cells for one parent's column of a worksheet.</summary>
public sealed record ParentCells(string Gross, string ChildSupport, string Alimony, string Childcare, string Healthcare);

/// <summary>Where a worksheet line's Plaintiff / Defendant / Combined values live; null when the form has no such cell.</summary>
public sealed record LineCells(string? Plaintiff, string? Defendant, string? Combined);

/// <summary>
/// Describes one embedded official workbook: which resource it is, which sheet is the form, and the cell addresses
/// of everything FairShare writes (inputs, names) or reads back in tests (every worksheet line).
/// </summary>
/// <param name="ResourceName">Manifest resource name of the embedded .xlsx.</param>
/// <param name="SheetName">The worksheet that is the form (the workbooks also carry the schedule and a print sheet).</param>
/// <param name="FileStem">Human form name used in the download filename, e.g. "CS-42".</param>
/// <param name="ChildrenCell">The "Number of Children" input.</param>
/// <param name="PlaintiffNameCell">Top-left cell of the merged plaintiff-name range on the caption line.</param>
/// <param name="DefendantNameCell">Top-left cell of the merged defendant-name range on the caption line.</param>
/// <param name="Lines">Line number -> cells, in form order. Documents the mapping and drives the oracle test; the exporter never writes these.</param>
public sealed record WorksheetTemplate(
    string State,
    string Form,
    string ResourceName,
    string SheetName,
    string FileStem,
    string ChildrenCell,
    string PlaintiffNameCell,
    string DefendantNameCell,
    ParentCells Plaintiff,
    ParentCells Defendant,
    IReadOnlyDictionary<string, LineCells> Lines);

/// <summary>
/// Registry of the official workbooks FairShare can fill. Adding a form = embed its workbook under
/// <c>Templates/{state}/</c> (file name without dashes so the manifest name is predictable) and describe its cells here.
/// </summary>
public static class WorksheetTemplates
{
    private const string ResourcePrefix = "FairShare.Api.Templates.";

    // Alabama Form CS-42, Rev. 5/2022 - sheet "Form CS-42 Worksheet".
    public static readonly WorksheetTemplate AlabamaCS42 = new(
        State: "AL",
        Form: "CS42",
        ResourceName: ResourcePrefix + "AL.CS42.xlsx",
        SheetName: "Form CS-42 Worksheet",
        FileStem: "CS-42",
        ChildrenCell: "K12",
        PlaintiffNameCell: "B6",
        DefendantNameCell: "F6",
        Plaintiff: new ParentCells(Gross: "H14", ChildSupport: "H15", Alimony: "H16", Childcare: "H20", Healthcare: "H21"),
        Defendant: new ParentCells(Gross: "J14", ChildSupport: "J15", Alimony: "J16", Childcare: "J20", Healthcare: "J21"),
        Lines: new Dictionary<string, LineCells>
        {
            ["1"] = new("H14", "J14", "L14"),
            ["1a"] = new("H15", "J15", "L15"),
            ["1b"] = new("H16", "J16", "L16"),
            ["2"] = new("H17", "J17", "L17"),
            ["3"] = new("H18", "J18", "L18"),
            ["4"] = new(null, null, "L19"),
            ["5"] = new("H20", "J20", "L20"),
            ["6"] = new("H21", "J21", "L21"),
            ["7"] = new(null, null, "L22"),
            ["8"] = new("H23", "J23", null),
            ["9"] = new("H24", "J24", null),
            ["10"] = new("H25", "J25", null),
            ["11"] = new("H27", "J27", null),
            ["12"] = new("H28", "J28", null),
            ["13"] = new("H30", "J30", null),
        });

    // Alabama Form CS-42-S, Eff. 6/2023 - sheet "Form CS-42-S".
    public static readonly WorksheetTemplate AlabamaCS42S = new(
        State: "AL",
        Form: "CS42S",
        ResourceName: ResourcePrefix + "AL.CS42S.xlsx",
        SheetName: "Form CS-42-S",
        FileStem: "CS-42-S",
        ChildrenCell: "K12",
        PlaintiffNameCell: "B6",
        DefendantNameCell: "F6",
        Plaintiff: new ParentCells(Gross: "H14", ChildSupport: "H15", Alimony: "H16", Childcare: "H22", Healthcare: "H23"),
        Defendant: new ParentCells(Gross: "J14", ChildSupport: "J15", Alimony: "J16", Childcare: "J22", Healthcare: "J23"),
        Lines: new Dictionary<string, LineCells>
        {
            ["1"] = new("H14", "J14", "L14"),
            ["1a"] = new("H15", "J15", "L15"),
            ["1b"] = new("H16", "J16", "L16"),
            ["2"] = new("H17", "J17", "L17"),
            ["3"] = new("H19", "J19", "L19"),
            ["4"] = new(null, null, "L20"),
            ["5"] = new(null, null, "L21"),
            ["6"] = new("H22", "J22", null),
            ["7"] = new("H23", "J23", null),
            ["8"] = new("H24", "J24", "L24"),
            ["9"] = new(null, null, "L25"),
            ["10"] = new("H26", "J26", null),
            ["11"] = new("H28", "J28", null),
            ["12"] = new("H29", "J29", null),
            ["13"] = new("H30", "J30", null),
            ["14"] = new("H32", "J32", null),
        });

    public static IReadOnlyList<WorksheetTemplate> All { get; } = [AlabamaCS42, AlabamaCS42S];

    public static WorksheetTemplate? Find(string state, string form)
        => All.FirstOrDefault(t =>
            t.State.Equals(state, StringComparison.OrdinalIgnoreCase) &&
            t.Form.Equals(form, StringComparison.OrdinalIgnoreCase));
}
