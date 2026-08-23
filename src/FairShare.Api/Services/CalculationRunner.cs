using System;
using System.Linq;
using FairShare.Contracts.Calculation;
using FairShare.Domain.Calculators;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Api.Services;

/// <summary>
/// The outcome of dispatching one calculation request. <see cref="FormFound"/> false maps to 404;
/// a non-null <see cref="InputError"/> maps to 400; otherwise <see cref="Response"/> is set (its
/// own Success flag distinguishes computed results from validation errors inside the worksheet).
/// </summary>
public sealed record CalculationRun(bool FormFound, string? InputError, CalculationResponse? Response);

/// <summary>
/// Runs a calculation request against the right calculator for a (state, form) - the shared engine
/// behind the calculations endpoint and saved scenarios (which recompute on save and on reopen).
/// </summary>
public interface ICalculationRunner
{
    CalculationRun Run(string state, string form, CalculationRequest request);
}

public sealed class CalculationRunner(IStateGuidelineCatalog catalog) : ICalculationRunner
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    public CalculationRun Run(string state, string form, CalculationRequest request)
    {
        IWorksheetForm? formEntry = _catalog.GetForm(state, form);

        if (formEntry is IChildSupportCalculator calculator)
        {
            CalculationResult result = calculator.Calculate(
                ToParentData(request.Plaintiff), ToParentData(request.Defendant), request.NumberOfChildren);

            return new CalculationRun(true, null, ToResponse(result));
        }

        if (formEntry is OregonWorksheetCalculator oregonCalculator)
        {
            if (request.Oregon is null)
            {
                return new CalculationRun(true, $"{state}/{form} requires the request's 'oregon' inputs.", null);
            }

            if (!TryMapOregonInput(request.Oregon, out OregonWorksheetInput? oregonInput, out string? mappingError))
            {
                return new CalculationRun(true, mappingError, null);
            }

            OregonCalculationOutcome outcome = oregonCalculator.Calculate(oregonInput!);
            CalculationResult result = ToCalculationResult(oregonCalculator, request.Oregon, outcome);

            return new CalculationRun(true, null, ToResponse(result, ToOregonDto(outcome)));
        }

        return new CalculationRun(false, null, null);
    }

    /// <summary>
    /// Folds an Oregon outcome into the shared result shape: the headline payer is line 7c's
    /// parent (empty when neither pays for the minors) and the headline amount is that parent's
    /// line 9e total; both parents' totals travel in the Oregon extras.
    /// </summary>
    private static CalculationResult ToCalculationResult(OregonWorksheetCalculator calculator, OregonCalculationRequest request, OregonCalculationOutcome outcome)
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
