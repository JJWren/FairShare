using System;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Api.Models;

/// <summary>
/// A Scenario (see CONTEXT.md): a named snapshot of one worksheet's inputs for a state and form,
/// stamped with the rule version and result it was computed under (ADR 0006). The inputs and
/// snapshot are stored as the wire JSON of the calculation contract, which keeps the schema
/// state-agnostic - Alabama and Oregon scenarios share this table unchanged.
/// </summary>
public class SavedScenario
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    public string State { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Form { get; set; } = string.Empty;

    /// <summary>The CalculationRequest JSON the scenario was saved with.</summary>
    [Required]
    public string InputsJson { get; set; } = string.Empty;

    /// <summary>
    /// The CalculationResponse JSON (without worksheet lines) the inputs produced at save time -
    /// the number the user saw, preserved verbatim so a reopen can say whether today's rules moved it.
    /// </summary>
    [Required]
    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>The guideline effective date the snapshot was computed under (ISO date), when the form has one.</summary>
    [MaxLength(10)]
    public string? RuleEffectiveDate { get; set; }

    /// <summary>Scenarios are strictly owned - they exist only for signed-in users and die with the account.</summary>
    public Guid OwnerUserId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
