using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FairShare.Api.Auth;
using FairShare.Api.Persistence;
using FairShare.Api.Models;
using FairShare.Api.Services;
using FairShare.Api.Services.Export;
using FairShare.Api.Options;
using FairShare.Api.Middleware;
using FairShare.Api.Observability;
using Microsoft.Extensions.Logging;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Calculators;
using FairShare.Domain.Services;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Required by the parameterless UseExceptionHandler() below (Production pipeline);
// also gives unhandled errors an RFC 7807 problem-details body.
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "FairShare API", Version = "v1" });

    // Swashbuckle 10 rides Microsoft.OpenApi 2.x: the Models namespace folded into
    // Microsoft.OpenApi, schemes are referenced through OpenApiSecuritySchemeReference, and the
    // requirement is built per document.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Connection string not found.");

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

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey must be configured.")
    .ValidateOnStart();

builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminSeed"));

var authenticationBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

// Google sign-in (ADR 0004): the API drives the code flow because the client secret lives
// here. Registered only when configured - unconfigured instances simply have no public
// sign-up (GET /auth/config tells the SPA). The External cookie is single-purpose
// transport between the Google callback and /auth/google/complete.
string? googleClientId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrWhiteSpace(googleClientId))
{
    authenticationBuilder
        .AddCookie(FairShare.Api.Controllers.AuthController.ExternalScheme, options =>
        {
            options.Cookie.Name = "fairshare_external";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        })
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
            options.SignInScheme = FairShare.Api.Controllers.AuthController.ExternalScheme;
            // Minimal data (ADR 0004): openid + email; the profile scope (name, photo) is
            // deliberately not requested - we would not store it anyway.
            options.Scope.Remove("profile");
        });
}

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((jwtBearerOptions, jwtOptionsAccessor) =>
    {
        JwtOptions jwtOptions = jwtOptionsAccessor.Value;

        jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("NotGuest", p => p.RequireAssertion(ctx =>
        !ctx.User.HasClaim(c => c.Type == "guest" && c.Value == "true")));
});

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
    {
        // With no configured origins, leave the policy empty rather than calling
        // WithOrigins([]): browsers get no CORS headers (cross-origin blocked), while
        // same-origin and non-browser clients (curl/Postman) are unaffected.
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  // The SPA names downloaded files after the server's Content-Disposition.
                  .WithExposedHeaders("Content-Disposition");
        }
    });
});

// Abuse throttling. Values are deliberately hardcoded (small self-hosted app); the only
// knob is the RateLimiting:Enabled kill-switch checked at UseRateLimiter below. Keys are
// the direct peer IP on purpose: only X-Forwarded-Proto is trusted from proxies (see the
// ForwardedHeaders comment), so X-Forwarded-For must not drive partitioning - behind a
// reverse proxy all clients share the proxy's bucket until KnownProxies is pinned.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        ctx.Request.Path.StartsWithSegments("/healthz")
            // The compose healthcheck polls this endpoint; it must never be throttled.
            ? System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("healthz")
            : System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

    // Credential stuffing / junk-account / token-minting surface: login, register,
    // guest, refresh.
    options.AddPolicy("auth", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        return ValueTask.CompletedTask;
    };
});

// DataProtection keys (Identity's password-reset/confirmation tokens) default to the
// container's own filesystem, which is lost on every recreate. Point them at a volume
// (DataProtection:KeysPath, e.g. /data/keys in the compose stacks) so they survive.
string? dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("FairShare");
}

// Behind a TLS-terminating reverse proxy (the eventual VPS setup), the app sees plain
// HTTP; honoring X-Forwarded-Proto keeps Request.IsHttps - and everything derived from
// it, like the refresh cookie's Secure/SameSite attributes - correct. The proxy address
// isn't known ahead of time for a self-hosted app, so no KnownProxies restriction; a
// client spoofing the header only affects its own cookie attributes.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Domain services - every worksheet form registers as IWorksheetForm; the catalog narrows the
// classic two-parent forms back to IChildSupportCalculator itself.
builder.Services.AddScoped<IWorksheetForm, CS42Calculator>();
builder.Services.AddScoped<IWorksheetForm, CS42SCalculator>();
builder.Services.AddScoped<IWorksheetForm, OregonWorksheetCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<ICalculationRunner, CalculationRunner>();
builder.Services.AddScoped<IScenarioService, ScenarioService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IWorksheetExporter, ClosedXmlWorksheetExporter>();
builder.Services.AddScoped<IParentProfileService, ParentProfileService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

// Observability (ADR 0003): first-party log persistence, audit trail, and analytics.
// The provider is registered through DI so the sink can share its channel instance;
// appsettings gives the "Sqlite" alias a permissive framework filter and the
// LogLevelSwitch does the real (runtime-adjustable) filtering.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<LogLevelSwitch>();
builder.Services.AddSingleton<SqliteLoggerProvider>();
builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<SqliteLoggerProvider>());
builder.Services.AddHostedService<LogSink>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<AnalyticsSecretProvider>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddHostedService<ObservabilityMaintenanceService>();

var app = builder.Build();

if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning(
        "Cors:AllowedOrigins is empty - browser clients on other origins (e.g. the FairShare.Web app) will be blocked until it is configured.");
}

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
                // A brand-new database (no applied migrations) has nothing worth backing up,
                // and a failed backup must not abort the migration - running against an
                // unmigrated schema is strictly worse than migrating without a backup.
                if (db.Database.GetAppliedMigrations().Any())
                {
                    try
                    {
                        string backupDir = Path.Combine(Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory, "backups");
                        BackupSqliteDatabase(dbPath, backupDir);
                    }
                    catch (Exception backupEx)
                    {
                        app.Logger.LogWarning(backupEx, "Pre-migration backup failed; continuing with migration.");
                    }
                }

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

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseForwardedHeaders();

// The app itself only serves TLS in Development (dotnet run's https profile). Behind the
// reverse proxy it listens on plain HTTP and UseHttpsRedirection has no HTTPS port to
// redirect to - it would just log "Failed to determine the https port for redirect" on
// the container healthcheck's own request. Wire it only when Kestrel serves TLS
// (Development) or an HTTPS port is configured explicitly (HttpsRedirection:HttpsPort /
// ASPNETCORE_HTTPS_PORT); the proxy already forces HTTPS for real clients.
// ASPNETCORE_HTTPS_PORT reaches configuration both as HTTPS_PORT (host config strips the
// prefix) and under its full name; accept any of the three keys, but only a real port
// number - a blank or garbage value must not enable a redirect the middleware can't build.
static bool IsValidPort(string? value) => int.TryParse(value?.Trim(), out int port) && port > 0 && port <= 65535;
bool httpsPortConfigured =
    IsValidPort(builder.Configuration["HttpsRedirection:HttpsPort"]) ||
    IsValidPort(builder.Configuration["HTTPS_PORT"]) ||
    IsValidPort(builder.Configuration["ASPNETCORE_HTTPS_PORT"]);
if (app.Environment.IsDevelopment() || httpsPortConfigured)
{
    app.UseHttpsRedirection();
}

app.UseCors("Web");

// After UseCors so a 429 still carries CORS headers (otherwise the SPA sees an opaque
// CORS failure instead of the status). Enabled is a kill-switch for tests/operators.
if (builder.Configuration.GetValue("RateLimiting:Enabled", true))
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<UserActivityMiddleware>();

app.MapControllers();

// Liveness probe for container orchestration (compose healthcheck, reverse proxies, monitors).
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

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
    // Timestamp alone collides when two instances start in the same second (e.g. parallel
    // test hosts); the random suffix keeps every backup name unique.
    string stamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..24];
    string backupFile = Path.Combine(backupDir, $"fairshare_{stamp}.db");
    File.Copy(dbPath, backupFile, overwrite: false);
    string zipPath = backupFile + ".zip";
    using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    zip.CreateEntryFromFile(backupFile, Path.GetFileName(backupFile));
    File.Delete(backupFile);
}

public partial class Program;
