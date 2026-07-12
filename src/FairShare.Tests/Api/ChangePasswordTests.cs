using FairShare.Contracts.Admin;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class ChangePasswordTests : IClassFixture<FairShareApiFactory>
{
    private const string AdminPassword = "Adm!n-Test-12345";

    // No automatic cookie handling: these tests juggle refresh cookies from several
    // "sessions" of the same user, which a shared cookie container would silently merge.
    private readonly HttpClient _client;

    public ChangePasswordTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessions_AndKeepsCurrentOneAlive()
    {
        await CreateUserAsync("alice", "Password-1");

        // Two independent sessions for alice.
        (AuthTokenResponse _, string otherSessionCookie) = await LoginAsync("alice", "Password-1");
        (AuthTokenResponse currentTokens, string _) = await LoginAsync("alice", "Password-1");

        HttpResponseMessage changeResponse = await SendChangePasswordAsync(
            currentTokens.AccessToken, "Password-1", "Password-2");

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        AuthTokenResponse? newTokens = await changeResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(newTokens);
        Assert.False(string.IsNullOrWhiteSpace(newTokens!.AccessToken));

        // The other session's refresh cookie must be dead...
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshWithCookieAsync(otherSessionCookie));

        // ...while the cookie issued by the change-password response still works.
        string newCookie = ExtractRefreshCookie(changeResponse);
        Assert.Equal(HttpStatusCode.OK, await RefreshWithCookieAsync(newCookie));

        // And only the new password logs in.
        Assert.Equal(HttpStatusCode.Unauthorized, await TryLoginAsync("alice", "Password-1"));
        Assert.Equal(HttpStatusCode.OK, await TryLoginAsync("alice", "Password-2"));
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsValidationProblem()
    {
        await CreateUserAsync("bob", "Password-1");
        (AuthTokenResponse tokens, string _) = await LoginAsync("bob", "Password-1");

        HttpResponseMessage response = await SendChangePasswordAsync(
            tokens.AccessToken, "definitely-wrong-1", "Password-2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PasswordMismatch", body);

        // The password did not change.
        Assert.Equal(HttpStatusCode.OK, await TryLoginAsync("bob", "Password-1"));
    }

    [Fact]
    public async Task ChangePassword_WithGuestToken_ReturnsForbidden()
    {
        HttpResponseMessage guestResponse = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse guestTokens = (await guestResponse.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        HttpResponseMessage response = await SendChangePasswordAsync(
            guestTokens.AccessToken, "irrelevant-1", "Password-2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "irrelevant-1",
            NewPassword = "Password-2",
            ConfirmNewPassword = "Password-2"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task CreateUserAsync(string userName, string password)
    {
        (AuthTokenResponse adminTokens, string _) = await LoginAsync("admin", AdminPassword);

        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/admin/users")
        {
            Content = JsonContent.Create(new CreateUserRequest
            {
                UserName = userName,
                Password = password,
                ConfirmPassword = password,
                Role = "User"
            })
        };
        request.Headers.Add("Authorization", $"Bearer {adminTokens.AccessToken}");

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(AuthTokenResponse Tokens, string RefreshCookie)> LoginAsync(string userName, string password)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return (tokens, ExtractRefreshCookie(response));
    }

    private async Task<HttpStatusCode> TryLoginAsync(string userName, string password)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> SendChangePasswordAsync(string accessToken, string currentPassword, string newPassword)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmNewPassword = newPassword
            })
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        return await _client.SendAsync(request);
    }

    private async Task<HttpStatusCode> RefreshWithCookieAsync(string refreshCookie)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/auth/refresh");
        request.Headers.Add("Cookie", refreshCookie);

        HttpResponseMessage response = await _client.SendAsync(request);
        return response.StatusCode;
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        string cookie = Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));
        return cookie.Split(';')[0];
    }
}
