using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FairShare.Client.Pages;
using FairShare.Server.Components;
using FairShare.Server.Components.Account;
using FairShare.Server.Data;
using FairShare.Shared.Models;
using FairShare.Shared.Interfaces;
using FairShare.Shared.Calculators;
using FairShare.Shared.Services;
using FairShare.Server.Services;
using FairShare.Server.Options;
using FairShare.Server.Middleware;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("NotGuest", p => p.RequireAssertion(ctx =>
        !ctx.User.HasClaim(c => c.Type == "guest" && c.Value == "true")));
});

var connectionString = builder.Configuration.GetConnectionString("Default") ?? 
                       builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                       throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<FairShareDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FairShareDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Domain services
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<IParentProfileService, ParentProfileService>();
builder.Services.AddScoped<ICalculatorRegistry, CalculatorRegistry>();
builder.Services.AddScoped<AdminSeeder>();

builder.Services.AddControllers();

builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminSeed"));

var app = builder.Build();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.UseMiddleware<UserActivityMiddleware>();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FairShare.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Identity API for WASM
app.MapIdentityApi<ApplicationUser>();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await seeder.SeedAsync();
}

app.Run();

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
