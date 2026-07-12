using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

public class RateLimitEnabledApiFactory : FairShareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        SetEnvVar("RateLimiting__Enabled", "true");
    }
}

[Collection("Api")]
public class RateLimitingTests : IClassFixture<RateLimitEnabledApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitingTests(RateLimitEnabledApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task AuthEndpoint_Burst_GetsThrottledWith429AndRetryAfter()
    {
        // A nonexistent username so Identity lockout state is never touched; the request
        // still counts against the "auth" policy. TestServer reports no RemoteIpAddress,
        // so every request lands in the same "unknown" partition. 21 requests guarantee a
        // rejection even if the burst straddles a fixed-window boundary (max 10+10 pass).
        var statuses = new List<HttpResponseMessage>();

        for (int i = 0; i < 21; i++)
        {
            statuses.Add(await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
            {
                UserName = "no-such-user",
                Password = "irrelevant-1"
            }));
        }

        HttpResponseMessage? limited = statuses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.NotNull(limited);
        Assert.True(limited!.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.Equal("60", Assert.Single(retryAfter!));

        // The liveness probe must never be throttled (compose healthcheck).
        HttpResponseMessage health = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
