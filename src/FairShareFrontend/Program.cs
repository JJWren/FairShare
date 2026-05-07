using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FairShareShared.Interfaces;
using FairShareShared.Calculators;
using FairShareShared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Domain services for client-side calculations
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<ICalculatorRegistry, CalculatorRegistry>();

await builder.Build().RunAsync();


