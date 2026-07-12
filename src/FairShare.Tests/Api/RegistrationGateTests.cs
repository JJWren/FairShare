using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class RegistrationGateTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public RegistrationGateTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Register_ByDefault_ReturnsForbiddenProblem()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/register", new RegisterRequest
        {
            UserName = "newcomer",
            Password = "Password-1"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Self-registration is disabled", body);
    }

    [Fact]
    public async Task Config_ByDefault_ReportsSelfRegistrationDisabled()
    {
        AuthConfigResponse? config = await _client.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config");

        Assert.NotNull(config);
        Assert.False(config!.AllowSelfRegistration);
    }
}

public class RegistrationEnabledApiFactory : FairShareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        SetEnvVar("Auth__AllowSelfRegistration", "true");
    }
}

[Collection("Api")]
public class RegistrationEnabledTests : IClassFixture<RegistrationEnabledApiFactory>
{
    private readonly HttpClient _client;

    public RegistrationEnabledTests(RegistrationEnabledApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Register_WhenEnabled_IssuesTokensAndRefreshCookie()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/register", new RegisterRequest
        {
            UserName = "self-registered",
            Password = "Password-1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();

        Assert.NotNull(tokens);
        Assert.Equal("self-registered", tokens!.UserName);
        Assert.False(tokens.IsGuest);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Config_WhenEnabled_ReportsSelfRegistrationEnabled()
    {
        AuthConfigResponse? config = await _client.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config");

        Assert.NotNull(config);
        Assert.True(config!.AllowSelfRegistration);
    }
}
