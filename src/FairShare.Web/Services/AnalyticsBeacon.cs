using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace FairShare.Web.Services;

/// <summary>
/// First-party page-view beacon (ADR 0003). nginx serves this SPA, so route changes never
/// reach the API on their own; this posts them. Honors DNT/Global Privacy Control before
/// sending anything (the server checks again), sets no cookies or identifiers, and is
/// strictly fire-and-forget - analytics must never affect the visitor's experience.
/// </summary>
public sealed class AnalyticsBeacon(NavigationManager navigation, IHttpClientFactory httpClientFactory, IJSRuntime js) : IDisposable
{
    private readonly NavigationManager _navigation = navigation;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IJSRuntime _js = js;

    private bool _started;
    private bool _optedOut;

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            _optedOut = await _js.InvokeAsync<bool>("fairshareAnalytics.isOptedOut");
        }
        catch
        {
            // If the helper is unavailable (stale cached assets), err on the quiet side.
            _optedOut = true;
        }

        if (_optedOut)
        {
            return;
        }

        // Initial load carries the external referrer; SPA route changes have none.
        string? referrer = null;
        try
        {
            referrer = await _js.InvokeAsync<string?>("fairshareAnalytics.referrer");
        }
        catch
        {
            // Optional enrichment only.
        }

        await TrackPageAsync(_navigation.Uri, referrer);
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>Reports a guest running into a users-only feature (conversion-intent signal).</summary>
    public async Task TrackGatedHitAsync(string gate)
    {
        if (!_started || _optedOut)
        {
            return;
        }

        await PostQuietlyAsync("api/v1/analytics/events", new ClientEventRequest { Name = "gated-hit", Target = gate });
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        await TrackPageAsync(e.Location, referrer: null);

    private async Task TrackPageAsync(string uri, string? referrer)
    {
        string path = "/" + _navigation.ToBaseRelativePath(uri);
        await PostQuietlyAsync("api/v1/analytics/page-views", new PageViewRequest { Path = path, Referrer = referrer });
    }

    private async Task PostQuietlyAsync<T>(string route, T payload)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("Api");
            await client.PostAsJsonAsync(route, payload);
        }
        catch
        {
            // Ad-blockers and offline states are expected; the page must never notice.
        }
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
