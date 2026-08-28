using System.Linq;
using FairShare.Contracts.Calculation;
using FairShare.Domain.Helpers;

namespace FairShare.Api.Services.Forms;

/// <summary>Shared domain-result-to-wire mapping every form runner ends with.</summary>
internal static class WorksheetResponseMapper
{
    public static CalculationResponse ToResponse(CalculationResult result, OregonResultDto? oregon = null)
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
}
