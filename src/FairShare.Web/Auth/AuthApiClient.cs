using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FairShare.Web.Auth;

public class AuthApiClient(HttpClient http, ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider)
{
    private readonly HttpClient _http = http;
    private readonly ITokenStore _tokenStore = tokenStore;
    private readonly JwtAuthenticationStateProvider _authStateProvider = authStateProvider;
    private readonly object _configLock = new();
    private Task<AuthConfigResponse>? _configTask;

    /// <summary>
    /// Server auth capabilities (e.g. whether self-registration is open). Cached for the
    /// app's lifetime; concurrent callers share one request (the lock makes the
    /// check-and-assign atomic). Fails closed to "registration disabled" when the API is
    /// unreachable - the server enforces the flag regardless, so a wrongly hidden link is
    /// the safe failure - and drops the cache so a later page visit retries.
    /// </summary>
    public Task<AuthConfigResponse> GetAuthConfigAsync()
    {
        lock (_configLock)
        {
            return _configTask ??= FetchAuthConfigAsync();
        }
    }

    private async Task<AuthConfigResponse> FetchAuthConfigAsync()
    {
        try
        {
            AuthConfigResponse? config = await _http.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config");

            if (config is not null)
            {
                return config;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
        }

        lock (_configLock)
        {
            _configTask = null;
        }

        return new AuthConfigResponse();
    }

    public Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword, string confirmNewPassword) =>
        SendAuthRequestAsync(
            "api/v1/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmNewPassword = confirmNewPassword
            });

    public Task<AuthResult> LoginAsync(string userName, string password) =>
        SendAuthRequestAsync(
            "api/v1/auth/login",
            new LoginRequest { UserName = userName, Password = password },
            unauthorizedMessage: "Invalid username or password.");

    public Task<AuthResult> RegisterAsync(string userName, string password) =>
        SendAuthRequestAsync("api/v1/auth/register", new RegisterRequest { UserName = userName, Password = password });

    public Task<AuthResult> ContinueAsGuestAsync() =>
        SendAuthRequestAsync("api/v1/auth/guest", body: null);

    /// <summary>
    /// Attempts a silent refresh using the HttpOnly refresh cookie, e.g. to re-hydrate the
    /// in-memory access token after a page reload. Never throws; returns false when there is
    /// no valid cookie or the API is unreachable.
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        try
        {
            AuthResult result = await SendAuthRequestAsync("api/v1/auth/refresh", body: null);
            return result.Success;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

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

            string? problemDetail = await TryReadFirstProblemErrorAsync(response);
            return new AuthResult(false, problemDetail ?? $"Authentication request failed ({(int)response.StatusCode}).");
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

    // The API reports failures as RFC 7807 problem details; surface the first field error
    // (e.g. "Username 'x' is already taken.") so the user sees something actionable
    // instead of a bare status code. Internal so pages issuing raw HttpClient calls
    // (e.g. the admin reset-password form) can surface the same details.
    internal static async Task<string?> TryReadFirstProblemErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                    {
                        return field.Value[0].GetString();
                    }
                }
            }

            if (root.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            // Not a problem-details body (empty, HTML error page, etc.) - fall back to the status code.
        }

        return null;
    }
}

public record AuthResult(bool Success, string? Error);
