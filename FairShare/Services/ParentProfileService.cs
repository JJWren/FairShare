using FairShare.Data;
using FairShare.Interfaces;
using FairShare.Models;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Services;

public class ParentProfileService(FairShareDbContext db) : IParentProfileService
{
    private readonly FairShareDbContext _db = db;

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
            throw new InvalidOperationException("OwnerUserId must be set before creating a ParentProfile.");
        }

        if (profile.CreatedUtc == default)
        {
            profile.CreatedUtc = DateTime.UtcNow;
        }

        _db.ParentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<bool> UpdateAsync(ParentProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedUtc = DateTime.UtcNow;
        _db.ParentProfiles.Update(profile);

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
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

    public Task<ParentProfile?> FindDuplicateAsync(ParentData data, string? displayName, CancellationToken ct = default)
    {
        IQueryable<ParentProfile> q = _db.ParentProfiles.Where(p =>
            !p.IsArchived &&
            p.MonthlyGrossIncome == data.MonthlyGrossIncome &&
            p.PreexistingChildSupport == data.PreexistingChildSupport &&
            p.PreexistingAlimony == data.PreexistingAlimony &&
            p.WorkRelatedChildcareCosts == data.WorkRelatedChildcareCosts &&
            p.HealthcareCoverageCosts == data.HealthcareCoverageCosts);

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            string dn = displayName.Trim();
            q = q.Where(p => p.DisplayName == dn);
        }

        return q.OrderBy(p => p.CreatedUtc).FirstOrDefaultAsync(ct);
    }

    public async Task<ParentProfile> GetOrCreateAsync(ParentData data, string? displayNameHint, Guid? ownerUserId = null, CancellationToken ct = default)
    {
        ParentProfile? existing = await FindDuplicateAsync(data, displayNameHint, ct);

        if (existing is not null)
        {
            // If duplicate is unowned and caller passes an owner, bind it now.
            if (existing.OwnerUserId is null && ownerUserId is not null)
            {
                existing.OwnerUserId = ownerUserId;
                existing.UpdatedUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return existing;
        }

        string displayName = string.IsNullOrWhiteSpace(displayNameHint)
            ? $"Parent {DateTime.UtcNow:yyyyMMdd-HHmmss}"
            : displayNameHint.Trim();

        ParentProfile profile = new()
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
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
        await _db.SaveChangesAsync(ct);
        return profile;
    }
}
