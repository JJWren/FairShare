using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FairShare.Web;
using FairShare.Web.Auth;
using FairShare.Web.Services;

// The app is English-only and ships only the EFIGS ICU shard (see the csproj): pin the
// culture so every browser locale formats identically ($, en-US numbers) instead of a
// non-EFIGS locale meeting ICU data that doesn't cover it.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture =
        new System.Globalization.CultureInfo("en-US");

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("NotGuest", p => p.RequireAssertion(ctx =>
        !ctx.User.HasClaim(c => c.Type == "guest" && c.Value == "true")));
});
builder.Services.AddCascadingAuthenticationState();

// Singletons (not scoped): IHttpClientFactory builds message handlers in its own DI
// scope, so a scoped token store/state provider there would be a different instance
// than the one the UI uses - stateful in-memory auth requires the shared instance.
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<JwtAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddTransient<AuthTokenHandler>();

string apiBaseUrl = builder.Configuration["Api:BaseUrl"] is { Length: > 0 } configured
    ? configured
    : builder.HostEnvironment.BaseAddress;

builder.Services.AddHttpClient("Api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddSingleton<AnalyticsBeacon>();

WebAssemblyHost host = builder.Build();

// The access token lives only in memory, so a page reload loses it. Re-hydrate it from
// the HttpOnly refresh cookie before first render so an active session survives reloads.
// Visitors with no session at all get a guest one so the calculator is usable immediately
// (guest-first landing, ADR 0002); if the API is unreachable they stay anonymous and the
// router falls back to the login redirect.
AuthApiClient authApi = host.Services.GetRequiredService<AuthApiClient>();

if (!await authApi.TryRefreshAsync())
{
    await authApi.TryStartGuestSessionAsync();
}

// After auth bootstrap so the beacon's requests carry the session's own token (which is
// how the server excludes the admin's browsing). Deliberately not awaited: analytics
// must never delay first render.
_ = host.Services.GetRequiredService<AnalyticsBeacon>().StartAsync();

await host.RunAsync();
