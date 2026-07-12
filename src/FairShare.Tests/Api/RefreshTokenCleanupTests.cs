using System.Threading;
using FairShare.Api.Models;
using FairShare.Api.Persistence;
using FairShare.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class RefreshTokenCleanupTests : IClassFixture<FairShareApiFactory>
{
    private readonly FairShareApiFactory _factory;

    public RefreshTokenCleanupTests(FairShareApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteStale_RemovesExpiredAndOldRevoked_KeepsActiveAndRecentlyRevoked()
    {
        DateTime now = DateTime.UtcNow;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

            db.RefreshTokens.AddRange(
                NewToken("cleanup-expired", expiresUtc: now.AddDays(-10)),
                NewToken("cleanup-revoked-old", expiresUtc: now.AddDays(10), revokedUtc: now.AddDays(-8)),
                NewToken("cleanup-active", expiresUtc: now.AddDays(10)),
                NewToken("cleanup-revoked-recent", expiresUtc: now.AddDays(10), revokedUtc: now.AddDays(-1)));

            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

            int deleted = await RefreshTokenCleanupService.DeleteStaleAsync(db, DateTime.UtcNow, CancellationToken.None);
            Assert.Equal(2, deleted);

            List<string> remaining = await db.RefreshTokens
                .Where(t => t.TokenHash.StartsWith("cleanup-"))
                .Select(t => t.TokenHash)
                .ToListAsync();

            Assert.Equal(["cleanup-active", "cleanup-revoked-recent"], remaining.OrderBy(h => h).ToList());
        }
    }

    private static RefreshToken NewToken(string hash, DateTime expiresUtc, DateTime? revokedUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = null,
        IsGuest = true,
        TokenHash = hash,
        CreatedUtc = DateTime.UtcNow.AddDays(-30),
        ExpiresUtc = expiresUtc,
        RevokedUtc = revokedUtc
    };
}
