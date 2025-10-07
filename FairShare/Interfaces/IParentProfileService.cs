using FairShare.Models;

namespace FairShare.Interfaces;

public interface IParentProfileService
{
    Task<IReadOnlyList<ParentProfile>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<ParentProfile?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ParentProfile> CreateAsync(ParentProfile profile, CancellationToken ct = default);
    Task<bool> UpdateAsync(ParentProfile profile, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, CancellationToken ct = default);
    Task<ParentProfile?> FindDuplicateAsync(ParentData data, string? displayName, CancellationToken ct = default);
    Task<ParentProfile> GetOrCreateAsync(ParentData data, string? displayNameHint, CancellationToken ct = default);
}
