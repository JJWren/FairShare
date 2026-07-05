using FairShare.Contracts.Auth;
using FairShare.Contracts.Catalog;

namespace FairShare.Tests.Api;

public class AuthEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient();
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
    public async Task States_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.GetAsync("api/v1/states");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task States_WithGuestToken_ReturnsAlabama()
    {
        HttpResponseMessage guestResponse = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse tokens = (await guestResponse.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        using HttpRequestMessage request = new(HttpMethod.Get, "api/v1/states");
        request.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<StateSummaryDto>? states = await response.Content.ReadFromJsonAsync<List<StateSummaryDto>>();

        Assert.NotNull(states);
        Assert.Contains(states!, s => s.State == "AL");
    }
}
