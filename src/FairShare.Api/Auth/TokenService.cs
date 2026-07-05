using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Models;
using FairShare.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FairShare.Api.Auth;

public class TokenService(FairShareDbContext db, IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly FairShareDbContext _db = db;
    private readonly JwtOptions _options = jwtOptions.Value;

    public AccessToken IssueAccessToken(ApplicationUser? user, IReadOnlyList<string> roles, bool isGuest)
    {
        DateTime expiresUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        List<Claim> claims = [];

        if (isGuest)
        {
            claims.Add(new Claim(ClaimTypes.Name, "Guest"));
            claims.Add(new Claim("guest", "true"));
        }
        else if (user is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            claims.Add(new Claim(ClaimTypes.Name, user.UserName ?? string.Empty));

            foreach (string role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.SigningKey));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: creds);

        string value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, expiresUtc);
    }

    public async Task<string> IssueRefreshTokenAsync(Guid? userId, bool isGuest, CancellationToken ct = default)
    {
        string raw = GenerateRawToken();

        RefreshToken entity = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IsGuest = isGuest,
            TokenHash = Hash(raw),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        return raw;
    }

    public async Task<RefreshToken?> ConsumeRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        string hash = Hash(rawToken);
        RefreshToken? existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || !existing.IsActive)
        {
            return null;
        }

        existing.RevokedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return existing;
    }

    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string raw)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
