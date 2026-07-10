using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using FairShare.Domain.Models;

namespace FairShare.Api.Services;

public interface IParentProfileService
{
    Task<IReadOnlyList<ParentProfile>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<ParentProfile?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ParentProfile> CreateAsync(ParentProfile profile, CancellationToken ct = default);
    /// <param name="expectedRowVersion">
    /// When provided, the update only succeeds if the stored row still has this version
    /// (optimistic concurrency); a mismatch returns <c>false</c>.
    /// </param>
    Task<bool> UpdateAsync(ParentProfile profile, byte[]? expectedRowVersion = null, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// Creates the profile, or updates the caller's existing active profile with the same
    /// display name (case-insensitive) in place. Returns the profile and whether it was created.
    /// </summary>
    Task<(ParentProfile Profile, bool Created)> UpsertByNameAsync(ParentData data, string displayName, Guid? ownerUserId, CancellationToken ct = default);
}






