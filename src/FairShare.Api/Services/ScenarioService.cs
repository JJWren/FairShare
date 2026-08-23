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

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
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
