using System;

namespace FairShare.Api.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public bool IsGuest { get; set; }

    public bool IsActive => RevokedUtc is null && DateTime.UtcNow < ExpiresUtc;
}
