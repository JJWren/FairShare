using System;
using System.ComponentModel.DataAnnotations;
using FairShare.Contracts.Calculation;

namespace FairShare.Contracts.Scenarios;

/// <summary>
/// Saves (or updates, when the name already exists for this user) a Scenario: the full inputs of
/// one worksheet for one state and form. The server recomputes the result itself - the snapshot is
/// never taken from the client.
/// </summary>
public class ScenarioSaveRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    public string State { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Form { get; set; } = string.Empty;

    [Required]
    public CalculationRequest Inputs { get; set; } = new();
}

public class ScenarioSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;

    /// <summary>The saved result's headline: who pays ("Plaintiff"/"Defendant", empty for none) and how much.</summary>
    public string Payer { get; set; } = string.Empty;
    public int FinalAmount { get; set; }

    /// <summary>The guideline effective date the snapshot was computed under, when the form has one.</summary>
    public string? RuleEffectiveDate { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

/// <summary>
/// A reopened Scenario: the stored inputs and saved snapshot, plus the same inputs recomputed
/// under today's rules (ADR 0006 - a saved number is never silently changed, and never silently
/// stale). <see cref="Current"/> is null when the scenario's form is no longer registered.
/// </summary>
public class ScenarioDetailDto : ScenarioSummaryDto
{
    public CalculationRequest Inputs { get; set; } = new();
    public CalculationResponse Snapshot { get; set; } = new();
    public CalculationResponse? Current { get; set; }

    /// <summary>True when today's rules produce a different result than the saved snapshot.</summary>
    public bool ResultChanged { get; set; }
}
