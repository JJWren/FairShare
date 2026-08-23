using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Models;
using FairShare.Api.Services;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Scenarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

/// <summary>
/// Saved Scenarios (ADR 0006). A gated feature: guests calculate freely but only signed-in users
/// persist, so the whole controller requires a non-guest account. Scenarios are strictly
/// owner-scoped - there is no admin cross-access to other people's saved cases-in-planning.
/// </summary>
[Authorize(Policy = "NotGuest")]
[Route("api/v1/scenarios")]
[ApiController]
public class ScenariosController(IScenarioService scenarios, ICalculationRunner runner) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IScenarioService _scenarios = scenarios;
    private readonly ICalculationRunner _runner = runner;

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (CurrentUserId is not Guid uid)
        {
            return Forbid();
        }

        var list = await _scenarios.ListForOwnerAsync(uid, ct);
        return Ok(list.Select(ToSummary));
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ScenarioSaveRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not Guid uid)
        {
            return Forbid();
        }

        // The server computes the snapshot itself; a client-supplied result is never trusted.
        CalculationRun run = _runner.Run(request.State, request.Form, request.Inputs);

        if (!run.FormFound)
        {
            return NotFound(new { message = $"No calculator registered for {request.State}/{request.Form}." });
        }

        if (run.InputError is not null)
        {
            return BadRequest(new { message = run.InputError });
        }

        if (run.Response is not { Success: true } response)
        {
            // A scenario exists to preserve a number the user saw; inputs the worksheet rejects
            // have no number to preserve.
            return UnprocessableEntity(run.Response);
        }

        SavedScenario scenario = new()
        {
            Name = request.Name.Trim(),
            State = response.State,
            Form = response.Form,
            OwnerUserId = uid,
            InputsJson = JsonSerializer.Serialize(request.Inputs, Json),
            SnapshotJson = JsonSerializer.Serialize(WithoutLines(response), Json),
            RuleEffectiveDate = response.Oregon?.RuleEffectiveDate,
        };

        (SavedScenario saved, bool created) = await _scenarios.UpsertByNameAsync(scenario, ct);

        return created
            ? CreatedAtAction(nameof(Get), new { id = saved.Id }, ToSummary(saved))
            : Ok(ToSummary(saved));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        SavedScenario? scenario = await OwnedAsync(id, ct);

        if (scenario is null)
        {
            return NotFound();
        }

        CalculationRequest inputs = JsonSerializer.Deserialize<CalculationRequest>(scenario.InputsJson, Json) ?? new();
        CalculationResponse snapshot = JsonSerializer.Deserialize<CalculationResponse>(scenario.SnapshotJson, Json) ?? new();

        // ADR 0006: reopening always recomputes under the CURRENT rules and says so when the
        // number moved - a saved figure is never silently changed and never silently stale.
        CalculationRun run = _runner.Run(scenario.State, scenario.Form, inputs);
        CalculationResponse? current = run.FormFound && run.InputError is null ? run.Response : null;

        ScenarioDetailDto detail = new()
        {
            Inputs = inputs,
            Snapshot = snapshot,
            Current = current,
            ResultChanged = current is not null && ResultsDiffer(snapshot, current),
        };
        Populate(detail, scenario);

        return Ok(detail);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        SavedScenario? scenario = await OwnedAsync(id, ct);

        if (scenario is null)
        {
            return NotFound();
        }

        await _scenarios.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>The scenario when it exists and belongs to the caller; owner mismatches read as 404, never 403.</summary>
    private async Task<SavedScenario?> OwnedAsync(Guid id, CancellationToken ct)
    {
        SavedScenario? scenario = await _scenarios.GetAsync(id, ct);
        return scenario is not null && scenario.OwnerUserId == CurrentUserId ? scenario : null;
    }

    /// <summary>What "the number moved" means: the headline or either Oregon total differs.</summary>
    public static bool ResultsDiffer(CalculationResponse snapshot, CalculationResponse current)
        => snapshot.Payer != current.Payer
            || snapshot.FinalAmount != current.FinalAmount
            || snapshot.Oregon?.PlaintiffTotalSupport != current.Oregon?.PlaintiffTotalSupport
            || snapshot.Oregon?.DefendantTotalSupport != current.Oregon?.DefendantTotalSupport;

    private static CalculationResponse WithoutLines(CalculationResponse response) => new()
    {
        Success = response.Success,
        Errors = response.Errors,
        State = response.State,
        Form = response.Form,
        NumberOfChildren = response.NumberOfChildren,
        Payer = response.Payer,
        FinalAmount = response.FinalAmount,
        Oregon = response.Oregon,
        Lines = [],
    };

    private ScenarioSummaryDto ToSummary(SavedScenario scenario)
    {
        ScenarioSummaryDto dto = new();
        Populate(dto, scenario);
        return dto;
    }

    private void Populate(ScenarioSummaryDto dto, SavedScenario scenario)
    {
        CalculationResponse snapshot = JsonSerializer.Deserialize<CalculationResponse>(scenario.SnapshotJson, Json) ?? new();

        dto.Id = scenario.Id;
        dto.Name = scenario.Name;
        dto.State = scenario.State;
        dto.Form = scenario.Form;
        dto.Payer = snapshot.Payer;
        dto.FinalAmount = snapshot.FinalAmount;
        dto.RuleEffectiveDate = scenario.RuleEffectiveDate;
        dto.CreatedUtc = scenario.CreatedUtc;
        dto.UpdatedUtc = scenario.UpdatedUtc;
    }
}
