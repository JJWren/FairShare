using FairShare.Calculators;
using FairShare.Interfaces;
using FairShare.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddViewOptions(o => { /* place view conventions if needed */ });

builder.Services.AddProblemDetails();

// Calculators & catalog
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>(); // Changed from AddSingleton to AddScoped

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/500");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Status code pages (404, etc.)
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "calculator",
    pattern: "States/{state}/{form}",
    defaults: new { controller = "Support", action = "Index" });

app.MapControllerRoute(
    name: "stateForms",
    pattern: "States/{state}",
    defaults: new { controller = "States", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/healthz", () => Results.Ok("OK"));

app.Run();
