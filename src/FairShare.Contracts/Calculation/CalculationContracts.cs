using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Contracts.Calculation;

public class ParentDataDto
{
    public bool HasPrimaryCustody { get; set; }

    [Range(0, int.MaxValue)]
    public int MonthlyGrossIncome { get; set; }

    [Range(0, int.MaxValue)]
    public int PreexistingChildSupport { get; set; }

    [Range(0, int.MaxValue)]
    public int PreexistingAlimony { get; set; }

    [Range(0, int.MaxValue)]
    public int WorkRelatedChildcareCosts { get; set; }

    [Range(0, int.MaxValue)]
    public int HealthcareCoverageCosts { get; set; }

    public void ApplyFrom(ParentDataDto source)
    {
        HasPrimaryCustody = source.HasPrimaryCustody;
        MonthlyGrossIncome = source.MonthlyGrossIncome;
        PreexistingChildSupport = source.PreexistingChildSupport;
        PreexistingAlimony = source.PreexistingAlimony;
        WorkRelatedChildcareCosts = source.WorkRelatedChildcareCosts;
        HealthcareCoverageCosts = source.HealthcareCoverageCosts;
    }
}

public class CalculationRequest
{
    [Range(0, int.MaxValue)]
    public int NumberOfChildren { get; set; }

    [Required]
    public ParentDataDto Plaintiff { get; set; } = new();

    [Required]
    public ParentDataDto Defendant { get; set; } = new();
}

public class CalcErrorDto
{
    public string Code { get; set; } = "CALC_ERROR";
    public string Message { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string Severity { get; set; } = "Error";
}

public class CalculationResponse
{
    public bool Success { get; set; } = true;
    public List<CalcErrorDto> Errors { get; set; } = [];
    public string State { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public int NumberOfChildren { get; set; }
    public string Payer { get; set; } = string.Empty;
    public int FinalAmount { get; set; }
}
