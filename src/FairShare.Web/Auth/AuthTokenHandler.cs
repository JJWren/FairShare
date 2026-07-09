using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FairShare.Web.Auth;

public class AuthTokenHandler(ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider) : DelegatingHandler
{
    // Shared across handler instances so concurrent 401s serialize onto a single refresh
    // attempt instead of racing the API's rotate-on-use refresh token and logging the user out.
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

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

        if (request.RequestUri is { } uri &&
            uri.ToString().Contains("api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        string? newAccessToken = await RefreshAccessTokenAsync(accessToken, cancellationToken);

        if (newAccessToken is null)
        {
            return response;
        }

        response.Dispose();

        HttpRequestMessage retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(string? accessTokenUsedForFailedRequest, CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);

        try
        {
            // Another request may have already refreshed while we were waiting on the lock.
            string? current = await _tokenStore.GetAccessTokenAsync();

            if (!string.IsNullOrWhiteSpace(current) && current != accessTokenUsedForFailedRequest)
            {
                return current;
            }

            using HttpRequestMessage refreshRequest = new(HttpMethod.Post, "api/v1/auth/refresh");
            refreshRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using HttpResponseMessage refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);

            if (!refreshResponse.IsSuccessStatusCode)
            {
                await _tokenStore.ClearAsync();
                _authStateProvider.NotifyAuthenticationChanged();
                return null;
            }

            AuthTokenResponse? tokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(cancellationToken: cancellationToken);

            if (tokens is null)
            {
                await _tokenStore.ClearAsync();
                _authStateProvider.NotifyAuthenticationChanged();
                return null;
            }

            await _tokenStore.SetAccessTokenAsync(tokens.AccessToken);
            _authStateProvider.NotifyAuthenticationChanged();
            return tokens.AccessToken;
        }
        finally
        {
            RefreshLock.Release();
        }
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
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
