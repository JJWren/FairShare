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

        // By this point HttpClient has already resolved the request URI against BaseAddress,
        // so it's absolute; without one we can't build the refresh URI, so don't try.
        if (request.RequestUri is not { IsAbsoluteUri: true } uri)
        {
            return response;
        }

        // A 401 from the anonymous auth endpoints (login/register/guest/refresh) is a real
        // auth failure (bad credentials, disabled user, etc.), not an expired-token
        // situation - don't let it trigger a refresh+retry, which could otherwise turn a
        // failed login into an "authenticated" state. change-password is the exception:
        // it's the one *authenticated* endpoint under the auth prefix, so an expired access
        // token there should get the normal silent refresh+retry like any other API call.
        if (uri.AbsolutePath.Contains("/api/v1/auth/", StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.EndsWith("/change-password", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        // The refresh request bypasses HttpClient (base.SendAsync goes straight to the inner
        // handler), so BaseAddress is never applied - derive an absolute URI from the failed
        // request instead of using a relative one that would resolve against the wrong origin.
        Uri refreshUri = new(uri, "/api/v1/auth/refresh");

        string? newAccessToken = await RefreshAccessTokenAsync(refreshUri, accessToken, cancellationToken);

        if (newAccessToken is null)
        {
            return response;
        }

        response.Dispose();

        HttpRequestMessage retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(Uri refreshUri, string? accessTokenUsedForFailedRequest, CancellationToken cancellationToken)
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

            using HttpRequestMessage refreshRequest = new(HttpMethod.Post, refreshUri);
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
        HttpRequestMessage clone = new(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };

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

        // Blazor stores per-request settings like BrowserRequestCredentials here; without
        // copying them, a retried request silently loses things such as "include cookies".
        foreach (var option in original.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
