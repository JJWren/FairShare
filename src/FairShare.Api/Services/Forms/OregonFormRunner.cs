using System;
using FairShare.Contracts.Calculation;
using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Models;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Api.Services.Forms;

/// <summary>
/// Runner for Oregon's Child Support Worksheet: validates and maps the request's Oregon
/// inputs (the form's shape doesn't fit <see cref="FairShare.Domain.Models.ParentData"/>),
/// runs the calculator, and folds the Oregon outcome into the shared response with the
/// Oregon extras attached.
/// </summary>
public sealed class OregonFormRunner(OregonWorksheetCalculator calculator) : IFormRunner
{
    private readonly OregonWorksheetCalculator _calculator = calculator;

    public string State => _calculator.State;
    public string Form => _calculator.Form;

    public FormRunResult Run(CalculationRequest request, string requestedState, string requestedForm)
    {
        if (request.Oregon is null)
        {
            // The caller's own casing, exactly as the pre-runner dispatch interpolated it.
            return new FormRunResult($"{requestedState}/{requestedForm} requires the request's 'oregon' inputs.", null);
        }

        if (!TryMapInput(request.Oregon, out OregonWorksheetInput? input, out string? mappingError))
        {
            return new FormRunResult(mappingError, null);
        }

        OregonCalculationOutcome outcome = _calculator.Calculate(input!);
        CalculationResult result = ToCalculationResult(request.Oregon, outcome);

        return new FormRunResult(null, WorksheetResponseMapper.ToResponse(result, ToOregonDto(outcome)));
    }

    /// <summary>
    /// Folds an Oregon outcome into the shared result shape: the headline payer is line 7c's
    /// parent (empty when neither pays for the minors) and the headline amount is that parent's
    /// line 9e total; both parents' totals travel in the Oregon extras.
    /// </summary>
    private CalculationResult ToCalculationResult(OregonCalculationRequest request, OregonCalculationOutcome outcome)
        => new(outcome.PaysForMinorChildren?.ToString() ?? string.Empty,
               // Line 9e is whole-dollar by construction (9a-9d each round to the dollar), so this
               // rounding is a guard against truncation, not a change of value.
               (int)Math.Round(outcome.PaysForMinorChildren switch
               {
                   ParentType.Plaintiff => outcome.PlaintiffTotalSupport,
                   ParentType.Defendant => outcome.DefendantTotalSupport,
                   _ => 0m,
               }, 0, MidpointRounding.AwayFromZero))
        {
            Success = outcome.Success,
            Errors = [.. outcome.Errors],
            State = State,
            Form = Form,
            NumberOfChildren = request.JointMinorChildren + request.JointChildrenAttendingSchool,
            Lines = outcome.Lines,
            // The outcome names the vintage it actually used - which may differ from Current
            // when the request pinned an AsOfDate.
            RuleVintage = $"OAR 137-050 effective {outcome.RuleEffectiveDate:yyyy-MM-dd}",
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

    private static bool TryMapInput(OregonCalculationRequest dto, out OregonWorksheetInput? input, out string? error)
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
            // Rule-vintage pin (null = current rules). Flows through scenario save/reopen
            // unchanged, so a scenario saved with a pinned date recomputes under that
            // vintage through this same path.
            AsOfDate = dto.AsOfDate,
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
}
