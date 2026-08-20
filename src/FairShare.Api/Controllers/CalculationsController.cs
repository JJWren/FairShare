using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Api.Services.Export;
using FairShare.Contracts.Calculation;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/states/{state}/forms/{form}/calculations")]
[Authorize]
public class CalculationsController(IStateGuidelineCatalog catalog, IWorksheetExporter exporter, IAnalyticsService analytics) : ControllerBase
{
    private readonly IStateGuidelineCatalog _catalog = catalog;
    private readonly IWorksheetExporter _exporter = exporter;
    private readonly IAnalyticsService _analytics = analytics;

    [HttpPost]
    public async Task<ActionResult<CalculationResponse>> Calculate(string state, string form, [FromBody] CalculationRequest request, CancellationToken ct)
    {
        IChildSupportCalculator? calculator = _catalog.GetCalculator(state, form);

        if (calculator is null)
        {
            return NotFound(new { message = $"No calculator registered for {state}/{form}." });
        }

        // Engagement telemetry (ADR 0003): the form key is the whole target - never inputs
        // or results. "started" = a calculation was attempted, "completed" = it succeeded.
        string eventTarget = $"{state}/{form}".ToLowerInvariant();
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationStarted, eventTarget, ct);

        ParentData plaintiff = ToParentData(request.Plaintiff);
        ParentData defendant = ToParentData(request.Defendant);

        CalculationResult result = calculator.Calculate(plaintiff, defendant, request.NumberOfChildren);

        if (result.Success)
        {
            await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationCompleted, eventTarget, ct);
        }

        return Ok(ToResponse(result));
    }

    /// <summary>
    /// The official worksheet workbook with these inputs typed into its input cells (formulas left live).
    /// The calculation must succeed first: an input the sheet cannot handle (e.g. income above the schedule)
    /// would otherwise come back as a workbook full of #N/A.
    /// </summary>
    [HttpPost("export/xlsx")]
    public IActionResult ExportXlsx(string state, string form, [FromBody] WorksheetExportRequest request)
    {
        IChildSupportCalculator? calculator = _catalog.GetCalculator(state, form);

        if (calculator is null || !_exporter.CanExport(state, form))
        {
            return NotFound(new { message = $"No worksheet export available for {state}/{form}." });
        }

        ParentData plaintiff = ToParentData(request.Plaintiff);
        ParentData defendant = ToParentData(request.Defendant);

        CalculationResult check = calculator.Calculate(plaintiff, defendant, request.NumberOfChildren);

        if (!check.Success)
        {
            return BadRequest(ToResponse(check));
        }

        WorksheetExport export = _exporter.Export(new WorksheetExportInput(
            state, form, request.NumberOfChildren, plaintiff, defendant, request.PlaintiffName, request.DefendantName));

        return File(export.Content, export.ContentType, export.FileName);
    }

    private static CalculationResponse ToResponse(CalculationResult result)
        => new()
        {
            Success = result.Success,
            State = result.State,
            Form = result.Form,
            NumberOfChildren = result.NumberOfChildren,
            Payer = result.Payer,
            FinalAmount = result.FinalAmount,
            Errors = result.Errors.Select(e => new CalcErrorDto
            {
                Code = e.Code,
                Message = e.Message,
                Field = e.Field,
                Severity = e.Severity.ToString()
            }).ToList(),
            Lines = result.Lines.Select(l => new WorksheetLineDto
            {
                Number = l.Number,
                Label = l.Label,
                Plaintiff = l.Plaintiff,
                Defendant = l.Defendant,
                Combined = l.Combined,
                Format = l.Format.ToString()
            }).ToList()
        };

    private static ParentData ToParentData(ParentDataDto dto) => new()
    {
        MonthlyGrossIncome = dto.MonthlyGrossIncome,
        PreexistingChildSupport = dto.PreexistingChildSupport,
        PreexistingAlimony = dto.PreexistingAlimony,
        WorkRelatedChildcareCosts = dto.WorkRelatedChildcareCosts,
        HealthcareCoverageCosts = dto.HealthcareCoverageCosts,
        HasPrimaryCustody = dto.HasPrimaryCustody
    };
}
