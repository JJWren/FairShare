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
    Task<ParentProfile?> FindDuplicateAsync(ParentData data, string? displayName, CancellationToken ct = default);
    Task<ParentProfile> GetOrCreateAsync(ParentData data, string? displayNameHint, Guid? ownerUserId = null, CancellationToken ct = default);
}






