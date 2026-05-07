using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FairShare.AppShared.Interfaces;
using FairShare.AppShared.Calculators;
using FairShare.AppShared.Services;

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









