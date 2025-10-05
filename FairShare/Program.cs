using FairShare.Calculators;
using FairShare.Interfaces;
using FairShare.Services;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();

// === Calculator services ===
builder.Services.AddSingleton<CS42SCalculator>();                           // AL CS-42-S engine
builder.Services.AddSingleton<IChildSupportCalculator, CS42SCalculator>();  // wraps CS42SCalculator
builder.Services.AddSingleton<ICalculatorRegistry, CalculatorRegistry>();   // global registry

WebApplication? app = builder.Build();

app.UseExceptionHandler("/error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// For non-exception status codes (404, 400, 403, etc.)
app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/healthz", () => Results.Ok("OK"));

app.Run();
