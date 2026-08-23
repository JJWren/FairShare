using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Api.Services;
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
public class CalculationsController(IStateGuidelineCatalog catalog, ICalculationRunner runner, IWorksheetExporter exporter, IAnalyticsService analytics) : ControllerBase
{
    private readonly IStateGuidelineCatalog _catalog = catalog;
    private readonly ICalculationRunner _runner = runner;
    private readonly IWorksheetExporter _exporter = exporter;
    private readonly IAnalyticsService _analytics = analytics;

    [HttpPost]
    public async Task<ActionResult<CalculationResponse>> Calculate(string state, string form, [FromBody] CalculationRequest request, CancellationToken ct)
    {
        // Engagement telemetry (ADR 0003): the form key is the whole target - never inputs
        // or results. "started" = a calculation was attempted, "completed" = it succeeded.
        string eventTarget = $"{state}/{form}".ToLowerInvariant();
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationStarted, eventTarget, ct);

        CalculationRun run = _runner.Run(state, form, request);

        if (!run.FormFound)
        {
            return NotFound(new { message = $"No calculator registered for {state}/{form}." });
        }

        if (run.InputError is not null)
        {
            return BadRequest(new { message = run.InputError });
        }

        if (run.Response!.Success)
        {
            await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationCompleted, eventTarget, ct);
        }

        return Ok(run.Response);
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

        CalculationRun check = _runner.Run(state, form, request);

        if (check.Response is not { Success: true })
        {
            return BadRequest(check.Response ?? new CalculationResponse { Success = false });
        }

        ParentData plaintiff = ToParentData(request.Plaintiff);
        ParentData defendant = ToParentData(request.Defendant);

        WorksheetExport export = _exporter.Export(new WorksheetExportInput(
            state, form, request.NumberOfChildren, plaintiff, defendant, request.PlaintiffName, request.DefendantName));

        return File(export.Content, export.ContentType, export.FileName);
    }

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
