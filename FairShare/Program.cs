using System.IO.Compression;

using FairShare.Calculators;
using FairShare.Data;
using FairShare.Interfaces;
using FairShare.Middleware;
using FairShare.Models;
using FairShare.Options;
using FairShare.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

static void BackupSqliteDatabase(string dbPath, string backupDir)
{
    if (!File.Exists(dbPath))
    {
        return;
    }

    Directory.CreateDirectory(backupDir);
    string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    string backupFile = Path.Combine(backupDir, $"fairshare_{stamp}.db");
    File.Copy(dbPath, backupFile, overwrite: false);
    string zipPath = backupFile + ".zip";
    using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    zip.CreateEntryFromFile(backupFile, Path.GetFileName(backupFile));
    File.Delete(backupFile);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AdminSeeder>();

builder.Services.AddControllersWithViews(options =>
{
    AuthorizationPolicy fallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(fallbackPolicy));
});

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();

// EF Core
builder.Services.AddDbContext<FairShareDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Domain services
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<IParentProfileService, ParentProfileService>();

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(opt =>
{
    opt.SignIn.RequireConfirmedAccount = false;
    opt.User.RequireUniqueEmail = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<FairShareDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    // Example placeholder for future guest logic:
    // o.Events.OnSigningIn = ctx => { /* modify ctx.Principal */ return Task.CompletedTask; };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"))
    .AddPolicy("NotGuest", p => p.RequireAssertion(ctx =>
        !ctx.User.HasClaim(c => c.Type == "guest" && c.Value == "true")));

builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminSeed"));

WebApplication app = builder.Build();

// Self-host upgrade safety
if (builder.Configuration.GetValue<bool>("AutoMigrate", true))
{
    using IServiceScope scope = app.Services.CreateScope();
    FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

    if (db.Database.IsSqlite())
    {
        string dbPath = db.Database.GetDbConnection().DataSource;

        try
        {
            using System.Data.Common.DbCommand cmd = db.Database.GetDbConnection().CreateCommand();
            db.Database.OpenConnection();
            cmd.CommandText = "PRAGMA integrity_check;";
            string? result = cmd.ExecuteScalar()?.ToString();

            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                app.Logger.LogError("SQLite integrity check failed: {Result}. Aborting migration.", result);
            }
            else if (db.Database.GetPendingMigrations().Any())
            {
                BackupSqliteDatabase(dbPath, Path.Combine(AppContext.BaseDirectory, "db_backups"));
                db.Database.Migrate();
                app.Logger.LogInformation("Applied pending migrations.");
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Migration sequence failed.");
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }
}

if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("ApplyMigrationsAtStartup"))
{
    using IServiceScope scope = app.Services.CreateScope();
    FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/500");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<UserActivityMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapRazorPages();

using (IServiceScope scope = app.Services.CreateScope())
{
    AdminSeeder seeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await seeder.SeedAsync();
}

// comment out to disable admin password reset at startup
//using (var scope = app.Services.CreateScope())
//{
//    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
//    var admin = await userMgr.FindByNameAsync("admin");
//    if (admin != null)
//    {
//        string newPwd = "Adm!nReset1!";
//        var token = await userMgr.GeneratePasswordResetTokenAsync(admin);
//        var result = await userMgr.ResetPasswordAsync(admin, token, newPwd);
//        if (!result.Succeeded)
//        {
//            Console.WriteLine("Reset failed: " + string.Join(';', result.Errors.Select(e => e.Description)));
//        }
//        else
//        {
//            Console.WriteLine($"Admin password reset to: {newPwd}");
//        }
//    }
//}

app.Run();
