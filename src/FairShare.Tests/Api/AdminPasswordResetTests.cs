using System.Text.Json;
using FairShare.Contracts.Admin;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class AdminPasswordResetTests : IClassFixture<FairShareApiFactory>
{
    private const string AdminPassword = "Adm!n-Test-12345";

    private readonly HttpClient _client;

    public AdminPasswordResetTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    [Fact]
    public async Task ResetPassword_ChangesPasswordAndRevokesSessions()
    {
        string adminToken = await LoginTokenAsync("admin", AdminPassword);
        Guid userId = await CreateUserAsync(adminToken, "carol", "Password-1");

        // A live session for carol that the reset must kill.
        HttpResponseMessage carolLogin = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "carol",
            Password = "Password-1"
        });
        Assert.Equal(HttpStatusCode.OK, carolLogin.StatusCode);
        string carolCookie = ExtractRefreshCookie(carolLogin);

        HttpResponseMessage resetResponse = await SendResetAsync(adminToken, userId, "Password-2");
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        // Pre-reset refresh cookie is dead.
        using HttpRequestMessage refresh = new(HttpMethod.Post, "api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", carolCookie);
        HttpResponseMessage refreshResponse = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        // Only the new password logs in.
        HttpResponseMessage oldLogin = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest { UserName = "carol", Password = "Password-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        HttpResponseMessage newLogin = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest { UserName = "carol", Password = "Password-2" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AsNonAdmin_ReturnsForbidden()
    {
        string adminToken = await LoginTokenAsync("admin", AdminPassword);
        Guid userId = await CreateUserAsync(adminToken, "dave", "Password-1");
        string daveToken = await LoginTokenAsync("dave", "Password-1");

        HttpResponseMessage response = await SendResetAsync(daveToken, userId, "Password-2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ForUnknownUser_ReturnsNotFound()
    {
        string adminToken = await LoginTokenAsync("admin", AdminPassword);

        HttpResponseMessage response = await SendResetAsync(adminToken, Guid.NewGuid(), "Password-2");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<string> LoginTokenAsync(string userName, string password)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<Guid> CreateUserAsync(string adminToken, string userName, string password)
    {
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
        request.Headers.Add("Authorization", $"Bearer {adminToken}");

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendResetAsync(string accessToken, Guid userId, string newPassword)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"api/v1/admin/users/{userId}/reset-password")
        {
            Content = JsonContent.Create(new AdminResetPasswordRequest
            {
                NewPassword = newPassword,
                ConfirmNewPassword = newPassword
            })
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        return await _client.SendAsync(request);
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        string cookie = Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));
        return cookie.Split(';')[0];
    }
}
