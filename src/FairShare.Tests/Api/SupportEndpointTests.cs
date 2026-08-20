using FairShare.Api.Persistence;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class SupportEndpointTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public SupportEndpointTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Donate_WithoutConfiguredUrl_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("go/donate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Config_WithoutConfiguredUrl_ReportsDonationsDisabled()
    {
        AuthConfigResponse config = (await _client.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config"))!;

        Assert.False(config.DonationsEnabled);
    }
}

public class DonationsEnabledApiFactory : FairShareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        SetEnvVar("Donations__BuyMeACoffeeUrl", "https://buymeacoffee.com/example");
    }
}

[Collection("Api")]
public class SupportEnabledEndpointTests : IClassFixture<DonationsEnabledApiFactory>
{
    private const string BrowserUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private readonly DonationsEnabledApiFactory _factory;
    private readonly HttpClient _client;

    public SupportEnabledEndpointTests(DonationsEnabledApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUa);
    }

    [Fact]
    public async Task Config_ReportsDonationsEnabled()
    {
        AuthConfigResponse config = (await _client.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config"))!;

        Assert.True(config.DonationsEnabled);
    }

    [Fact]
    public async Task Donate_RedirectsToConfiguredUrl_AndCountsTheClick()
    {
        HttpResponseMessage response = await _client.GetAsync("go/donate");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://buymeacoffee.com/example", response.Headers.Location!.ToString().TrimEnd('/'));

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.Equal(1, db.AnalyticsEvents.Count(e => e.Name == "donate-click"));
    }

    [Fact]
    public async Task Donate_WithDoNotTrack_StillRedirects_ButRecordsNothing()
    {
        int before;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            before = scope.ServiceProvider.GetRequiredService<FairShareDbContext>()
                .AnalyticsEvents.Count(e => e.Name == "donate-click");
        }

        HttpRequestMessage request = new(HttpMethod.Get, "go/donate");
        request.Headers.Add("DNT", "1");

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using IServiceScope verifyScope = _factory.Services.CreateScope();
        FairShareDbContext db = verifyScope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.Equal(before, db.AnalyticsEvents.Count(e => e.Name == "donate-click"));
    }
}
