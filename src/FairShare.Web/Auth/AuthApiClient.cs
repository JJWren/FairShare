using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FairShare.Contracts.Auth;

namespace FairShare.Web.Auth;

public class AuthApiClient(HttpClient http, ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider)
{
    private readonly HttpClient _http = http;
    private readonly ITokenStore _tokenStore = tokenStore;
    private readonly JwtAuthenticationStateProvider _authStateProvider = authStateProvider;

    public async Task<AuthResult> LoginAsync(string userName, string password)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/v1/auth/login", new LoginRequest { UserName = userName, Password = password });
        return await HandleTokenResponseAsync(response);
    }

    public async Task<AuthResult> RegisterAsync(string userName, string password)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("api/v1/auth/register", new RegisterRequest { UserName = userName, Password = password });
        return await HandleTokenResponseAsync(response);
    }

    public async Task<AuthResult> ContinueAsGuestAsync()
    {
        HttpResponseMessage response = await _http.PostAsync("api/v1/auth/guest", content: null);
        return await HandleTokenResponseAsync(response);
    }

    public async Task LogoutAsync()
    {
        string? refreshToken = await _tokenStore.GetRefreshTokenAsync();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _http.PostAsJsonAsync("api/v1/auth/logout", new RefreshRequest { RefreshToken = refreshToken });
        }

        await _tokenStore.ClearAsync();
        _authStateProvider.NotifyAuthenticationChanged();
    }

    private async Task<AuthResult> HandleTokenResponseAsync(HttpResponseMessage response)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return new AuthResult(false, "Invalid username or password.");
                return new AuthResult(false, $"Authentication request failed ({(int)response.StatusCode}).");
            }

            AuthTokenResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
            if (tokens is null) return new AuthResult(false, "Unexpected response from server.");

            await _tokenStore.SetTokensAsync(tokens.AccessToken, tokens.RefreshToken);
            _authStateProvider.NotifyAuthenticationChanged();
            return new AuthResult(true, null);
        }
    }
}

public record AuthResult(bool Success, string? Error);
