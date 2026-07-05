using System.Linq;
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
public class CalculationsController(IStateGuidelineCatalog catalog) : ControllerBase
{
    private readonly IStateGuidelineCatalog _catalog = catalog;

    [HttpPost]
    public ActionResult<CalculationResponse> Calculate(string state, string form, [FromBody] CalculationRequest request)
    {
        IChildSupportCalculator? calculator = _catalog.GetCalculator(state, form);

        if (calculator is null)
        {
            return NotFound(new { message = $"No calculator registered for {state}/{form}." });
        }

        ParentData plaintiff = ToParentData(request.Plaintiff);
        ParentData defendant = ToParentData(request.Defendant);

        CalculationResult result = calculator.Calculate(plaintiff, defendant, request.NumberOfChildren);

        return Ok(new CalculationResponse
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
            }).ToList()
        });
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
