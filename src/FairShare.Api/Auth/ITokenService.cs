using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Models;

namespace FairShare.Api.Auth;

public record AccessToken(string Value, DateTime ExpiresUtc);

/// <summary>Persistent = true gets a dated cookie; false means a session cookie (the server-side
/// row still expires, just sooner - see TokenService).</summary>
public record IssuedRefreshToken(string Value, DateTime ExpiresUtc, bool Persistent = true);

public interface ITokenService
{
    AccessToken IssueAccessToken(ApplicationUser? user, IReadOnlyList<string> roles, bool isGuest);

    Task<IssuedRefreshToken> IssueRefreshTokenAsync(Guid? userId, bool isGuest, bool persistent = true, CancellationToken ct = default);

    Task<RefreshToken?> ConsumeRefreshTokenAsync(string rawToken, CancellationToken ct = default);

    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
