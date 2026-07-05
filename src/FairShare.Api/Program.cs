using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FairShare.Api.Auth;
using FairShare.Api.Persistence;
using FairShare.Api.Models;
using FairShare.Api.Services;
using FairShare.Api.Options;
using FairShare.Api.Middleware;
using FairShare.Domain.Interfaces;
using FairShare.Domain.Calculators;
using FairShare.Domain.Services;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "FairShare API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    options.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

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
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Domain services
builder.Services.AddScoped<IChildSupportCalculator, CS42Calculator>();
builder.Services.AddScoped<IChildSupportCalculator, CS42SCalculator>();
builder.Services.AddScoped<IStateGuidelineCatalog, StateGuidelineCatalog>();
builder.Services.AddScoped<IParentProfileService, ParentProfileService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<AdminSeeder>();

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
                string backupDir = Path.Combine(Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory, "backups");
                BackupSqliteDatabase(dbPath, backupDir);
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

app.UseHttpsRedirection();

app.UseCors("Web");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<UserActivityMiddleware>();

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

public partial class Program;
