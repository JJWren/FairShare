using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FairShare.Web.Auth;

public class AuthApiClient(HttpClient http, ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider)
{
    private readonly HttpClient _http = http;
    private readonly ITokenStore _tokenStore = tokenStore;
    private readonly JwtAuthenticationStateProvider _authStateProvider = authStateProvider;

    public Task<AuthResult> LoginAsync(string userName, string password) =>
        SendAuthRequestAsync(
            "api/v1/auth/login",
            new LoginRequest { UserName = userName, Password = password },
            unauthorizedMessage: "Invalid username or password.");

    public Task<AuthResult> RegisterAsync(string userName, string password) =>
        SendAuthRequestAsync("api/v1/auth/register", new RegisterRequest { UserName = userName, Password = password });

    public Task<AuthResult> ContinueAsGuestAsync() =>
        SendAuthRequestAsync("api/v1/auth/guest", body: null);

    public async Task LogoutAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/auth/logout");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        using HttpResponseMessage response = await _http.SendAsync(request);

        await _tokenStore.ClearAsync();
        _authStateProvider.NotifyAuthenticationChanged();
    }

    // The refresh token travels exclusively via the HttpOnly cookie the API sets/reads,
    // so every auth call needs credentials included for that cookie to be stored/sent.
    private async Task<AuthResult> SendAuthRequestAsync(string url, object? body, string? unauthorizedMessage = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, url)
        {
            Content = body is not null ? JsonContent.Create(body) : null
        };
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        using HttpResponseMessage response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized && unauthorizedMessage is not null)
            {
                return new AuthResult(false, unauthorizedMessage);
            }

            return new AuthResult(false, $"Authentication request failed ({(int)response.StatusCode}).");
        }

        AuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();

        if (tokens is null)
        {
            return new AuthResult(false, "Unexpected response from server.");
        }

        await _tokenStore.SetAccessTokenAsync(tokens.AccessToken);
        _authStateProvider.NotifyAuthenticationChanged();
        return new AuthResult(true, null);
    }
}

public record AuthResult(bool Success, string? Error);
