using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Api.Observability;

/// <summary>
/// Caches the per-install HMAC secret, creating it lazily on first use. Two instances racing
/// the first insert is resolved by re-reading: the row that won is the truth for both.
/// </summary>
public class AnalyticsSecretProvider(IServiceScopeFactory scopeFactory)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[]? _secret;

    public async Task<byte[]> GetSecretAsync(CancellationToken ct = default)
    {
        if (_secret is not null)
        {
            return _secret;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_secret is not null)
            {
                return _secret;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

            AnalyticsState? state = await db.AnalyticsStates.SingleOrDefaultAsync(s => s.Id == 1, ct);

            if (state is null)
            {
                state = new AnalyticsState { Id = 1, SecretBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
                db.AnalyticsStates.Add(state);

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Another instance created it first; theirs wins.
                    db.Entry(state).State = EntityState.Detached;
                    state = await db.AnalyticsStates.SingleAsync(s => s.Id == 1, ct);
                }
            }

            _secret = Convert.FromBase64String(state.SecretBase64);
            return _secret;
        }
        finally
        {
            _gate.Release();
        }
    }
}
