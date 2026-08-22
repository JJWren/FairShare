using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Observability;
using FairShare.Api.Services.Export;
using FairShare.Contracts.Calculation;
using FairShare.Domain.Calculators;
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
        IWorksheetForm? formEntry = _catalog.GetForm(state, form);

        if (formEntry is null)
        {
            return NotFound(new { message = $"No calculator registered for {state}/{form}." });
        }

        // Engagement telemetry (ADR 0003): the form key is the whole target - never inputs
        // or results. "started" = a calculation was attempted, "completed" = it succeeded.
        string eventTarget = $"{state}/{form}".ToLowerInvariant();
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationStarted, eventTarget, ct);

        CalculationResult result;

        if (formEntry is IChildSupportCalculator calculator)
        {
            ParentData plaintiff = ToParentData(request.Plaintiff);
            ParentData defendant = ToParentData(request.Defendant);

            result = calculator.Calculate(plaintiff, defendant, request.NumberOfChildren);
        }
        else if (formEntry is OregonWorksheetCalculator oregonCalculator)
        {
            if (request.Oregon is null)
            {
                return BadRequest(new { message = $"{state}/{form} requires the request's 'oregon' inputs." });
            }

            if (!TryMapOregonInput(request.Oregon, out OregonWorksheetInput? oregonInput, out string? mappingError))
            {
                return BadRequest(new { message = mappingError });
            }

            OregonCalculationOutcome outcome = oregonCalculator.Calculate(oregonInput!);
            result = ToCalculationResult(oregonCalculator, request.Oregon, outcome);

            if (outcome.Success)
            {
                await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.CalculationCompleted, eventTarget, ct);
            }

            return Ok(ToResponse(result, ToOregonDto(outcome)));
        }
        else
        {
            return NotFound(new { message = $"No calculator registered for {state}/{form}." });
        }

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

    /// <summary>
    /// Folds an Oregon outcome into the shared result shape: the headline payer is line 7c's
    /// parent (empty when neither pays for the minors) and the headline amount is that parent's
    /// line 9e total; both parents' totals travel in the Oregon extras.
    /// </summary>
    private static CalculationResult ToCalculationResult(OregonWorksheetCalculator calculator, OregonCalculationRequest request, OregonCalculationOutcome outcome)
        => new(outcome.PaysForMinorChildren?.ToString() ?? string.Empty,
               (int)(outcome.PaysForMinorChildren switch
               {
                   Enums.ParentType.Plaintiff => outcome.PlaintiffTotalSupport,
                   Enums.ParentType.Defendant => outcome.DefendantTotalSupport,
                   _ => 0m,
               }))
        {
            Success = outcome.Success,
            Errors = [.. outcome.Errors],
            State = calculator.State,
            Form = calculator.Form,
            NumberOfChildren = request.JointMinorChildren + request.JointChildrenAttendingSchool,
            Lines = outcome.Lines,
        };

    private static OregonResultDto ToOregonDto(OregonCalculationOutcome outcome)
        => new()
        {
            PlaintiffTotalSupport = outcome.PlaintiffTotalSupport,
            DefendantTotalSupport = outcome.DefendantTotalSupport,
            PaysForMinorChildren = outcome.PaysForMinorChildren?.ToString(),
            CoverageProvider = outcome.CoverageProvider.ToString(),
            ReasonableCostTotal = outcome.ReasonableCostTotal,
            RuleEffectiveDate = outcome.RuleEffectiveDate.ToString("yyyy-MM-dd"),
        };

    private static bool TryMapOregonInput(OregonCalculationRequest dto, out OregonWorksheetInput? input, out string? error)
    {
        input = null;

        if (!Enum.TryParse(dto.CashMedical, ignoreCase: true, out CashMedicalElection cashMedical))
        {
            error = "cashMedical must be \"No\", \"Yes\", or \"Contingent\".";
            return false;
        }

        CoverageProvider? selection = null;
        if (dto.CoverageSelection is { } selectionText)
        {
            if (!Enum.TryParse(selectionText, ignoreCase: true, out CoverageProvider parsed))
            {
                error = "coverageSelection must be \"Plaintiff\", \"Defendant\", \"Both\", or \"EitherWhenAvailable\".";
                return false;
            }

            selection = parsed;
        }

        input = new OregonWorksheetInput
        {
            Plaintiff = ToOregonParent(dto.Plaintiff),
            Defendant = ToOregonParent(dto.Defendant),
            JointMinorChildren = dto.JointMinorChildren,
            JointChildrenAttendingSchool = dto.JointChildrenAttendingSchool,
            CashMedical = cashMedical,
            CoverageSelection = selection,
            OrderCoverageAtHigherAmount = dto.OrderCoverageAtHigherAmount,
        };
        error = null;
        return true;
    }

    private static OregonParentInput ToOregonParent(OregonParentDto dto) => new()
    {
        MonthlyIncome = dto.MonthlyIncome,
        SpousalSupportReceived = dto.SpousalSupportReceived,
        SpousalSupportPaid = dto.SpousalSupportPaid,
        UnionDues = dto.UnionDues,
        OwnHealthInsuranceCost = dto.OwnHealthInsuranceCost,
        NonJointChildren = dto.NonJointChildren,
        ChildCareCosts = dto.ChildCareCosts,
        ChildrensHealthCoverageCost = dto.ChildrensHealthCoverageCost,
        AverageOvernights = dto.AverageOvernights,
        SocialSecurityVeteransBenefits = dto.SocialSecurityVeteransBenefits,
        MinimumOrderException = dto.MinimumOrderException,
    };

    private static CalculationResponse ToResponse(CalculationResult result, OregonResultDto? oregon = null)
        => new()
        {
            Oregon = oregon,
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
