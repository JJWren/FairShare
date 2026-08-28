using FairShare.Contracts.Calculation;
using FairShare.Domain.Helpers;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Models;

namespace FairShare.Api.Services.Forms;

/// <summary>
/// Runner for the classic two-parents-plus-child-count shape (<see cref="IChildSupportCalculator"/> -
/// both Alabama forms). One instance wraps one calculator; a new state with this shape reuses
/// this class via a DI registration line and writes no new mapping code.
/// </summary>
public sealed class ClassicFormRunner(IChildSupportCalculator calculator) : IFormRunner
{
    private readonly IChildSupportCalculator _calculator = calculator;

    public string State => _calculator.State;
    public string Form => _calculator.Form;

    public FormRunResult Run(CalculationRequest request)
    {
        CalculationResult result = _calculator.Calculate(
            ToParentData(request.Plaintiff), ToParentData(request.Defendant), request.NumberOfChildren);

        return new FormRunResult(null, WorksheetResponseMapper.ToResponse(result));
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
