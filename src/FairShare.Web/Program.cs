using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FairShare.Web;
using FairShare.Web.Auth;

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

WebAssemblyHost host = builder.Build();

// The access token lives only in memory, so a page reload loses it. Re-hydrate it from
// the HttpOnly refresh cookie before first render so an active session survives reloads.
await host.Services.GetRequiredService<AuthApiClient>().TryRefreshAsync();

await host.RunAsync();
