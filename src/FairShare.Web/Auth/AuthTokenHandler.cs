using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Contracts.Auth;

namespace FairShare.Web.Auth;

public class AuthTokenHandler(ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider) : DelegatingHandler
{
    private readonly ITokenStore _tokenStore = tokenStore;
    private readonly JwtAuthenticationStateProvider _authStateProvider = authStateProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? accessToken = await _tokenStore.GetAccessTokenAsync();

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        string? refreshToken = await _tokenStore.GetRefreshTokenAsync();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return response;
        }

        HttpResponseMessage refreshResponse = await base.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(new RefreshRequest { RefreshToken = refreshToken })
            },
            cancellationToken);

        if (!refreshResponse.IsSuccessStatusCode)
        {
            await _tokenStore.ClearAsync();
            _authStateProvider.NotifyAuthenticationChanged();
            return response;
        }

        AuthTokenResponse? tokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(cancellationToken: cancellationToken);

        if (tokens is null)
        {
            await _tokenStore.ClearAsync();
            _authStateProvider.NotifyAuthenticationChanged();
            return response;
        }

        await _tokenStore.SetTokensAsync(tokens.AccessToken, tokens.RefreshToken);
        _authStateProvider.NotifyAuthenticationChanged();

        HttpRequestMessage retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            byte[] bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.Add(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.Add(header.Key, header.Value);
        }

        return clone;
    }
}
