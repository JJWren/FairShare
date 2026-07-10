using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Persistence;
using FairShare.Api.Models;
using FairShare.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Services;

public class ParentProfileService(FairShareDbContext db, ILogger<ParentProfileService> logger) : IParentProfileService
{
    private readonly FairShareDbContext _db = db;
    private readonly ILogger<ParentProfileService> _logger = logger;

    public async Task<IReadOnlyList<ParentProfile>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        IQueryable<ParentProfile> q = _db.ParentProfiles.Where(p => !p.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            q = q.Where(p => p.DisplayName.ToLower().Contains(term));
        }

        return await q.OrderBy(p => p.DisplayName)
                      .Take(100)
                      .ToListAsync(ct);
    }

    public Task<ParentProfile?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.ParentProfiles.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived, ct);

    public async Task<ParentProfile> CreateAsync(ParentProfile profile, CancellationToken ct = default)
    {
        if (profile.OwnerUserId is null)
        {
            _logger.LogWarning(
                "Creating ParentProfile {ProfileId} with DisplayName '{DisplayName}' without an OwnerUserId. " +
                "This is allowed for backward compatibility but should be avoided in new code.",
                profile.Id,
                profile.DisplayName);
        }

        if (profile.CreatedUtc == default)
        {
            profile.CreatedUtc = DateTime.UtcNow;
        }

        _db.ParentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<bool> UpdateAsync(ParentProfile profile, byte[]? expectedRowVersion = null, CancellationToken ct = default)
    {
        profile.UpdatedUtc = DateTime.UtcNow;
        _db.ParentProfiles.Update(profile);

        if (expectedRowVersion is not null)
        {
            // Enforce the caller's snapshot in the UPDATE's WHERE clause, so a write that
            // landed between our read and this save fails instead of being overwritten.
            _db.Entry(profile).Property(p => p.RowVersion).OriginalValue = expectedRowVersion;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Covers both an optimistic-concurrency mismatch (DbUpdateConcurrencyException)
            // and a rename colliding with the unique (owner, name) index.
            return false;
        }
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        ParentProfile? existing = await GetAsync(id, ct);

        if (existing is null)
        {
            return false;
        }

        existing.IsArchived = true;
        existing.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(ParentProfile Profile, bool Created)> UpsertByNameAsync(ParentData data, string displayName, Guid? ownerUserId, CancellationToken ct = default)
    {
        string name = displayName.Trim();

        ParentProfile? existing = await FindActiveByNameAsync(name, ownerUserId, ct);

        if (existing is not null)
        {
            return (await UpdateInPlaceAsync(existing, data, ownerUserId, ct), false);
        }

        ParentProfile profile = new()
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            MonthlyGrossIncome = data.MonthlyGrossIncome,
            PreexistingChildSupport = data.PreexistingChildSupport,
            PreexistingAlimony = data.PreexistingAlimony,
            WorkRelatedChildcareCosts = data.WorkRelatedChildcareCosts,
            HealthcareCoverageCosts = data.HealthcareCoverageCosts,
            HasPrimaryCustody = data.HasPrimaryCustody,
            CreatedUtc = DateTime.UtcNow,
            OwnerUserId = ownerUserId
        };

        _db.ParentProfiles.Add(profile);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a create race: the unique (OwnerUserId, lower(DisplayName)) index means
            // a concurrent request inserted this name between our check and our insert.
            // Fall back to updating the row that won.
            _db.Entry(profile).State = EntityState.Detached;

            ParentProfile? winner = await FindActiveByNameAsync(name, ownerUserId, ct);

            if (winner is null)
            {
                throw;
            }

            return (await UpdateInPlaceAsync(winner, data, ownerUserId, ct), false);
        }

        _logger.LogInformation(
            "Created new ParentProfile {ProfileId} ('{DisplayName}') for user {UserId}",
            profile.Id,
            profile.DisplayName,
            ownerUserId);

        return (profile, true);
    }

    // Within one user's saved parents, the display name acts as the natural key (enforced
    // by a unique index over OwnerUserId + lower(DisplayName) on active rows). Invariant
    // lowercasing on the C# side keeps matching culture-stable (e.g. Turkish-I); the EF
    // side stays ToLower() so it translates to SQL LOWER(), which is ASCII-only in SQLite
    // and agrees with invariant casing for ASCII names.
    private Task<ParentProfile?> FindActiveByNameAsync(string trimmedName, Guid? ownerUserId, CancellationToken ct)
    {
        string nameLower = trimmedName.ToLowerInvariant();

        return _db.ParentProfiles
            .Where(p => !p.IsArchived && p.OwnerUserId == ownerUserId && p.DisplayName.ToLower() == nameLower)
            .OrderBy(p => p.CreatedUtc)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<ParentProfile> UpdateInPlaceAsync(ParentProfile existing, ParentData data, Guid? ownerUserId, CancellationToken ct)
    {
        existing.ApplyFrom(data);
        existing.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated ParentProfile {ProfileId} ('{DisplayName}') in place for user {UserId}",
            existing.Id,
            existing.DisplayName,
            ownerUserId);

        return existing;
    }
}







