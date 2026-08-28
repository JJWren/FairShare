using FairShare.Contracts.Auth;
using FairShare.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class AuthEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly FairShareApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(FairShareApiFactory factory)
    {
        _factory = factory;

        // The refresh cookie is Secure, so the client's cookie container needs an https
        // base address or it silently drops the Set-Cookie response.
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // The cookie-acting auth endpoints require the CSRF header (the Web client always
        // sends it); harmless on every other request.
        _client.DefaultRequestHeaders.Add("X-FairShare-Auth", "1");
    }

    [Fact]
    public async Task Guest_IssuesTokenWithoutCredentials()
    {
        HttpResponseMessage response = await _client.PostAsync("api/v1/auth/guest", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();

        Assert.NotNull(tokens);
        Assert.True(tokens!.IsGuest);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_Succeeds()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();

        Assert.NotNull(tokens);
        Assert.Equal("Admin", tokens!.Role);
        Assert.False(tokens.IsGuest);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "definitely-wrong"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task States_WithoutToken_ReturnsOk()
    {
        // The catalog is deliberately anonymous so the homepage's state picker renders
        // without a session (Google OAuth verification: no login-gated landing page).
        HttpResponseMessage response = await _client.GetAsync("api/v1/states");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Guest_SetsHttpOnlyRefreshCookie_ScopedToAuthPath()
    {
        HttpResponseMessage response = await _client.PostAsync("api/v1/auth/guest", content: null);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        string cookie = Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guest_OverHttp_SetsCookieWithoutSecure_UsingSameSiteLax()
    {
        // SameSite=None requires Secure, which browsers drop over plain HTTP - fall back to
        // Secure=false/SameSite=Lax so local HTTP dev scenarios don't silently lose the cookie.
        using HttpClient httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        httpClient.DefaultRequestHeaders.Add("X-FairShare-Auth", "1");

        HttpResponseMessage response = await httpClient.PostAsync("api/v1/auth/guest", content: null);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        string cookie = Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));

        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_IssuesNewAccessToken()
    {
        // WebApplicationFactory's default client carries cookies across requests automatically.
        HttpResponseMessage guestResponse = await _client.PostAsync("api/v1/auth/guest", content: null);
        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);

        HttpResponseMessage refreshResponse = await _client.PostAsync("api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        AuthTokenResponse? refreshedTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();

        Assert.NotNull(refreshedTokens);
        Assert.True(refreshedTokens!.IsGuest);
        Assert.False(string.IsNullOrWhiteSpace(refreshedTokens.AccessToken));
    }

    [Fact]
    public async Task Refresh_WithSameCookieTwice_SecondAttemptFails()
    {
        // The API rotates (revokes-on-use) refresh tokens, so replaying the same cookie
        // value after it's already been consumed once must fail. Use a client with no
        // automatic cookie handling so the replayed (stale) value isn't silently replaced
        // by the container's own tracking of the rotated cookie.
        using HttpClient rawClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        rawClient.DefaultRequestHeaders.Add("X-FairShare-Auth", "1");

        HttpResponseMessage guestResponse = await rawClient.PostAsync("api/v1/auth/guest", content: null);
        Assert.True(guestResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        string refreshCookie = Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));
        string cookieValue = refreshCookie.Split(';')[0];

        using HttpRequestMessage firstRequest = new(HttpMethod.Post, "api/v1/auth/refresh");
        firstRequest.Headers.Add("Cookie", cookieValue);
        HttpResponseMessage firstRefresh = await rawClient.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        using HttpRequestMessage replay = new(HttpMethod.Post, "api/v1/auth/refresh");
        replay.Headers.Add("Cookie", cookieValue);
        HttpResponseMessage secondRefresh = await rawClient.SendAsync(replay);

        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsync("api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieActingEndpoints_WithoutCsrfHeader_ReturnsBadRequest()
    {
        // A fresh client WITHOUT the default header stands in for a cross-site form post:
        // guest/refresh/logout all set or act on the refresh cookie and must refuse it.
        using HttpClient bare = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsync("api/v1/auth/guest", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsync("api/v1/auth/refresh", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsync("api/v1/auth/logout", content: null)).StatusCode);
    }

    [Fact]
    public async Task States_WithGuestToken_ReturnsRegisteredStatesWithDisplayNames()
    {
        HttpResponseMessage guestResponse = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse tokens = (await guestResponse.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        using HttpRequestMessage request = new(HttpMethod.Get, "api/v1/states");
        request.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<StateSummaryDto>? states = await response.Content.ReadFromJsonAsync<List<StateSummaryDto>>();

        Assert.NotNull(states);
        // Codes carry the routing; DisplayName is what the landing picker shows.
        Assert.Contains(states!, s => s.State == "AL" && s.DisplayName == "Alabama");
        Assert.Contains(states!, s => s.State == "OR" && s.DisplayName == "Oregon");
    }
}
