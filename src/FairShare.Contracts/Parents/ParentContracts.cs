using System;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Contracts.Parents;

public class ParentProfileDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool HasPrimaryCustody { get; set; }
    public int MonthlyGrossIncome { get; set; }
    public int PreexistingChildSupport { get; set; }
    public int PreexistingAlimony { get; set; }
    public int WorkRelatedChildcareCosts { get; set; }
    public int HealthcareCoverageCosts { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public string? RowVersion { get; set; }
}

public class ParentProfileCreateRequest
{
    public string? DisplayName { get; set; }

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

    public bool HasPrimaryCustody { get; set; }

    public bool Deduplicate { get; set; } = true;
}

public class ParentProfileUpdateRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

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

    public bool HasPrimaryCustody { get; set; }

    public string? RowVersion { get; set; }
}
