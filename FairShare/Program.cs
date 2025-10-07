using FairShare.Calculators;
using FairShare.Interfaces;
using FairShare.Services;
using FairShare.Data;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

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
    // Optional: compress older backups
    string zipPath = backupFile + ".zip";
    using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    zip.CreateEntryFromFile(backupFile, Path.GetFileName(backupFile));
    File.Delete(backupFile);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddViewOptions(o => { /* place view conventions if needed */ });

builder.Services.AddProblemDetails();

// EF Core (SQLite)
builder.Services.AddDbContext<FairShareDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Domain services
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<IParentProfileService, ParentProfileService>();

WebApplication app = builder.Build();

// Self-host upgrade safety
if (builder.Configuration.GetValue<bool>("AutoMigrate", true))
{
    using IServiceScope scope = app.Services.CreateScope();
    FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

    if (db.Database.IsSqlite())
    {
        string dbPath = db.Database.GetDbConnection().DataSource;

        // Basic integrity check before touching schema
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
    db.Database.Migrate(); // No EnsureDeleted(); no EnsureCreated() once migrations exist.
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
