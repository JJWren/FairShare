using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Tests.Api;

// The forwarded-headers trust decides who the rate limiter throttles (its buckets key on
// Connection.RemoteIpAddress) and what scheme cookie attributes derive from, so these
// tests pin the trust rules themselves: X-Forwarded-For rewrites the caller only when
// the peer is inside a configured ForwardedHeaders:KnownNetworks CIDR, and never when
// the pin is absent. TestServer connections have no peer address, so a first-in-pipeline
// startup filter fakes one per request from the X-Test-Peer header; a test-only
// controller (registered via an application part, never part of the API assembly)
// echoes what the pipeline ended up with.

/// <summary>Echoes connection facts after the forwarded-headers middleware has run.</summary>
[ApiController]
public class ForwardingEchoController : ControllerBase
{
    [HttpGet("__test/remote-ip")]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new ForwardingEcho
    {
        RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
        IsHttps = Request.IsHttps
    });
}

public class ForwardingEcho
{
    public string? RemoteIp { get; set; }
    public bool IsHttps { get; set; }
}

/// <summary>
/// Runs before the app's own pipeline: stamps the fake TCP peer for the request so the
/// forwarded-headers middleware has a real address to judge trust against.
/// </summary>
public sealed class FakePeerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (ctx, nxt) =>
        {
            if (ctx.Request.Headers.TryGetValue("X-Test-Peer", out var peer)
                && IPAddress.TryParse(peer.ToString(), out IPAddress? address))
            {
                ctx.Connection.RemoteIpAddress = address;
            }

            await nxt();
        });
        next(app);
    };
}

public class TrustedProxyApiFactory : FairShareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        SetEnvVar("ForwardedHeaders__KnownNetworks__0", "172.16.0.0/12");
        builder.ConfigureServices(AddForwardingTestSurface);
    }

    internal static void AddForwardingTestSurface(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, FakePeerStartupFilter>();
        services.AddControllers().AddApplicationPart(typeof(ForwardingEchoController).Assembly);
    }
}

public class DefaultForwardingApiFactory : FairShareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(TrustedProxyApiFactory.AddForwardingTestSurface);
    }
}

// Separate test classes per factory: the factories configure the host through
// process-wide environment variables, so the two hosts must never be built while the
// other's variables are still set. The shared "Api" collection serializes the classes,
// and each fixture restores its variables on dispose before the next class starts.

[Collection("Api")]
public class ForwardedHeadersTrustedProxyTests : IClassFixture<TrustedProxyApiFactory>
{
    private readonly HttpClient _client;

    public ForwardedHeadersTrustedProxyTests(TrustedProxyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<ForwardingEcho> EchoAsync(string peer, params (string Name, string Value)[] headers)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "__test/remote-ip");
        request.Headers.Add("X-Test-Peer", peer);
        foreach ((string name, string value) in headers)
        {
            request.Headers.Add(name, value);
        }

        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ForwardingEcho>())!;
    }

    [Fact]
    public async Task XffFromPinnedNetwork_RewritesCallerToRealClient()
    {
        ForwardingEcho echo = await EchoAsync("172.18.0.5", ("X-Forwarded-For", "203.0.113.7"));

        Assert.Equal("203.0.113.7", echo.RemoteIp);
    }

    [Fact]
    public async Task XffFromOutsidePinnedNetwork_IsIgnored()
    {
        ForwardingEcho echo = await EchoAsync("8.8.8.8", ("X-Forwarded-For", "203.0.113.7"));

        Assert.Equal("8.8.8.8", echo.RemoteIp);
    }

    [Fact]
    public async Task XForwardedProtoHttps_MakesRequestHttps()
    {
        ForwardingEcho echo = await EchoAsync("172.18.0.5", ("X-Forwarded-Proto", "https"));

        Assert.True(echo.IsHttps);
    }

    [Fact]
    public async Task ApiResponses_CarryNosniff()
    {
        HttpResponseMessage response = await _client.GetAsync("__test/remote-ip");

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }
}

[Collection("Api")]
public class ForwardedHeadersDefaultTests : IClassFixture<DefaultForwardingApiFactory>
{
    private readonly HttpClient _client;

    public ForwardedHeadersDefaultTests(DefaultForwardingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WithoutPinnedNetworks_XffIsIgnoredEvenFromPrivatePeer()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "__test/remote-ip");
        request.Headers.Add("X-Test-Peer", "172.18.0.5");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        ForwardingEcho echo = (await response.Content.ReadFromJsonAsync<ForwardingEcho>())!;

        Assert.Equal("172.18.0.5", echo.RemoteIp);
    }
}
