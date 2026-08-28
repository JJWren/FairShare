using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Models;
using FairShare.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Api.Services;

public interface IScenarioService
{
    Task<IReadOnlyList<SavedScenario>> ListForOwnerAsync(Guid ownerUserId, CancellationToken ct = default);
    Task<SavedScenario?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Saves a scenario, updating in place when the owner already has one with this name -
    /// the name is the natural key within one user's scenarios, like saved parents.
    /// </summary>
    Task<(SavedScenario Scenario, bool Created)> UpsertByNameAsync(SavedScenario scenario, CancellationToken ct = default);

    /// <summary>
    /// Renames a scenario. Fails with <see cref="ScenarioRenameOutcome.NameTaken"/> when the
    /// owner already has a DIFFERENT scenario under the new name (case-insensitive) - a rename
    /// never merges or overwrites another scenario the way save's upsert-by-name does.
    /// </summary>
    Task<ScenarioRenameOutcome> RenameAsync(Guid id, string newName, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public enum ScenarioRenameOutcome
{
    NotFound,
    NameTaken,
    Renamed
}

public class ScenarioService(FairShareDbContext db) : IScenarioService
{
    private readonly FairShareDbContext _db = db;

    public async Task<IReadOnlyList<SavedScenario>> ListForOwnerAsync(Guid ownerUserId, CancellationToken ct = default)
        => await _db.Scenarios
            .Where(s => s.OwnerUserId == ownerUserId)
            .OrderByDescending(s => s.UpdatedUtc ?? s.CreatedUtc)
            .Take(100)
            .ToListAsync(ct);

    public Task<SavedScenario?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<(SavedScenario Scenario, bool Created)> UpsertByNameAsync(SavedScenario scenario, CancellationToken ct = default)
    {
        // Case-insensitive match so "Trial" and "trial" never accumulate as twins; the DB's
        // unique (OwnerUserId, Name) index backstops exact duplicates under concurrency.
        string nameLower = scenario.Name.ToLowerInvariant();
        SavedScenario? existing = await _db.Scenarios.FirstOrDefaultAsync(
            s => s.OwnerUserId == scenario.OwnerUserId && s.Name.ToLower() == nameLower, ct);

        if (existing is null)
        {
            _db.Scenarios.Add(scenario);
            await _db.SaveChangesAsync(ct);
            return (scenario, true);
        }

        existing.Name = scenario.Name;
        existing.State = scenario.State;
        existing.Form = scenario.Form;
        existing.InputsJson = scenario.InputsJson;
        existing.SnapshotJson = scenario.SnapshotJson;
        existing.RuleEffectiveDate = scenario.RuleEffectiveDate;
        existing.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (existing, false);
    }

    public async Task<ScenarioRenameOutcome> RenameAsync(Guid id, string newName, CancellationToken ct = default)
    {
        SavedScenario? existing = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (existing is null)
        {
            return ScenarioRenameOutcome.NotFound;
        }

        // Same case-insensitive natural key as UpsertByNameAsync; renaming a scenario to its
        // OWN name (a pure case change, "trial" -> "Trial") is allowed via the Id exclusion.
        string nameLower = newName.ToLowerInvariant();
        bool taken = await _db.Scenarios.AnyAsync(
            s => s.OwnerUserId == existing.OwnerUserId && s.Id != id && s.Name.ToLower() == nameLower, ct);

        if (taken)
        {
            return ScenarioRenameOutcome.NameTaken;
        }

        existing.Name = newName;
        existing.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ScenarioRenameOutcome.Renamed;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        SavedScenario? existing = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (existing is null)
        {
            return false;
        }

        _db.Scenarios.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
