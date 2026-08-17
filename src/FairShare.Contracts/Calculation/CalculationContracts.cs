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

/// <summary>
/// One numbered line of the worksheet, with the value shown in each column. A null column means the form has no
/// cell there. <see cref="Format"/> is "Currency" (whole dollars) or "Percent" (fraction, 0.57 = 57%).
/// </summary>
public class WorksheetLineDto
{
    public string Number { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? Plaintiff { get; set; }
    public decimal? Defendant { get; set; }
    public decimal? Combined { get; set; }
    public string Format { get; set; } = "Currency";
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

    /// <summary>
    /// Every worksheet line in form order; empty when <see cref="Success"/> is false.
    /// </summary>
    public List<WorksheetLineDto> Lines { get; set; } = [];
}
