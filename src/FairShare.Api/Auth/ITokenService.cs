using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Models;

namespace FairShare.Api.Auth;

public record AccessToken(string Value, DateTime ExpiresUtc);

public interface ITokenService
{
    AccessToken IssueAccessToken(ApplicationUser? user, IReadOnlyList<string> roles, bool isGuest);

    Task<string> IssueRefreshTokenAsync(Guid? userId, bool isGuest, CancellationToken ct = default);

    Task<RefreshToken?> ConsumeRefreshTokenAsync(string rawToken, CancellationToken ct = default);
}
