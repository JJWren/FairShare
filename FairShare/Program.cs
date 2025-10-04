WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();

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
    name: "default",
    pattern: "{controller=CS42S}/{action=Index}/{id?}");

app.MapGet("/healthz", () => Results.Ok("OK"));

app.Run();
