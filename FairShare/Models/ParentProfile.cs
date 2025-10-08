using System.ComponentModel.DataAnnotations;

namespace FairShare.Models;

public class ParentProfile : ParentData
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Guid? OwnerUserId { get; set; }    // nullable for legacy existing rows

    public void ApplyFrom(ParentData source)
    {
        MonthlyGrossIncome = source.MonthlyGrossIncome;
        PreexistingChildSupport = source.PreexistingChildSupport;
        PreexistingAlimony = source.PreexistingAlimony;
        WorkRelatedChildcareCosts = source.WorkRelatedChildcareCosts;
        HealthcareCoverageCosts = source.HealthcareCoverageCosts;
        HasPrimaryCustody = source.HasPrimaryCustody;
    }

    public ParentData ToParentData()
        => new()
        {
            MonthlyGrossIncome = MonthlyGrossIncome,
            PreexistingChildSupport = PreexistingChildSupport,
            PreexistingAlimony = PreexistingAlimony,
            WorkRelatedChildcareCosts = WorkRelatedChildcareCosts,
            HealthcareCoverageCosts = HealthcareCoverageCosts,
            HasPrimaryCustody = HasPrimaryCustody
        };
}
