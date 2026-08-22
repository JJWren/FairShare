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

    /// <summary>
    /// The Oregon worksheet's inputs. Required when calculating an Oregon form; ignored (and the
    /// classic fields above unused) otherwise.
    /// </summary>
    public OregonCalculationRequest? Oregon { get; set; }
}

/// <summary>One parent's column of the Oregon worksheet. Monthly dollars, cents allowed.</summary>
public class OregonParentDto
{
    [Range(0, double.MaxValue)]
    public decimal MonthlyIncome { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SpousalSupportReceived { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SpousalSupportPaid { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnionDues { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OwnHealthInsuranceCost { get; set; }

    [Range(0, int.MaxValue)]
    public int NonJointChildren { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ChildCareCosts { get; set; }

    /// <summary>Cost to enroll the joint children in this parent's coverage; null = no appropriate coverage available.</summary>
    [Range(0, double.MaxValue)]
    public decimal? ChildrensHealthCoverageCost { get; set; }

    [Range(0, 365)]
    public decimal AverageOvernights { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SocialSecurityVeteransBenefits { get; set; }

    public bool MinimumOrderException { get; set; }
}

/// <summary>The Oregon worksheet's case-level inputs plus both parents' columns.</summary>
public class OregonCalculationRequest
{
    [Required]
    public OregonParentDto Plaintiff { get; set; } = new();

    [Required]
    public OregonParentDto Defendant { get; set; } = new();

    [Range(0, int.MaxValue)]
    public int JointMinorChildren { get; set; }

    [Range(0, int.MaxValue)]
    public int JointChildrenAttendingSchool { get; set; }

    /// <summary>"No", "Yes", or "Contingent" (worksheet line 5a).</summary>
    public string CashMedical { get; set; } = "No";

    /// <summary>
    /// "Plaintiff", "Defendant", "Both", or "EitherWhenAvailable" (worksheet line 4f); null lets
    /// the calculator choose per OAR 137-050-0750.
    /// </summary>
    public string? CoverageSelection { get; set; }

    public bool OrderCoverageAtHigherAmount { get; set; }
}

/// <summary>
/// The Excel export takes the same figures as a calculation plus the optional names printed on the form's caption line.
/// </summary>
public class WorksheetExportRequest : CalculationRequest
{
    [MaxLength(100)]
    public string? PlaintiffName { get; set; }

    [MaxLength(100)]
    public string? DefendantName { get; set; }
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
/// cell there. <see cref="Format"/> is "Currency" (dollar amounts), "Percent" (fraction, 0.57 = 57%),
/// or "Number" (plain counts - children, overnights).
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

    /// <summary>Oregon-only extras; null for other states' forms.</summary>
    public OregonResultDto? Oregon { get; set; }
}

/// <summary>
/// What Oregon's worksheet produces beyond a single payer and amount: both parents can owe at once
/// (Children Attending School are paid directly by each parent), and every estimate names the rule
/// version it implements.
/// </summary>
public class OregonResultDto
{
    /// <summary>Line 9e for the plaintiff.</summary>
    public decimal PlaintiffTotalSupport { get; set; }

    /// <summary>Line 9e for the defendant.</summary>
    public decimal DefendantTotalSupport { get; set; }

    /// <summary>Line 7c: "Plaintiff" or "Defendant", or null when neither should pay for the minors.</summary>
    public string? PaysForMinorChildren { get; set; }

    /// <summary>Lines 4f/9f: "Plaintiff", "Defendant", "Both", or "EitherWhenAvailable".</summary>
    public string CoverageProvider { get; set; } = string.Empty;

    /// <summary>Line 9g: the reasonable cost cap to name in the order.</summary>
    public decimal ReasonableCostTotal { get; set; }

    /// <summary>The OAR 137-050 effective date this estimate implements (ISO date).</summary>
    public string RuleEffectiveDate { get; set; } = string.Empty;
}
