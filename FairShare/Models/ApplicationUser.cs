using Microsoft.AspNetCore.Identity;

namespace FairShare.Models;

[Flags]
public enum UserPermissionFlags
{
    None = 0,
    BasicAccess = 1 << 0,
    CreateContent = 1 << 1,
    EditOwnContent = 1 << 2,
    DeleteOwnContent = 1 << 3,
    // Future granular permissions:
    // ViewAnalytics = 1 << 4,
    // etc.
    AllBasic = BasicAccess | CreateContent | EditOwnContent | DeleteOwnContent
}

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public bool IsDisabled { get; set; }
    public UserPermissionFlags Permissions { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? DisplayName { get; set; }
}
