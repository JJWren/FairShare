using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

public class FairShareApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fairshare-tests-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, string?> _replacedEnvVars = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Environment variables, not ConfigureAppConfiguration: with minimal-hosting apps,
        // config added through ConfigureAppConfiguration lands BEFORE the app's own
        // appsettings sources, so appsettings.Development.json silently overrode these -
        // every test run shared one persistent bin/fairshare.db instead of the per-fixture
        // temp file (dotnet/aspnetcore#37680). Environment variables rank above appsettings
        // in WebApplication.CreateBuilder's precedence, so they always win. The API test
        // classes run sequentially (see ApiTestCollection), so fixtures can't race on them.
        SetEnvVar("ConnectionStrings__Default", $"Data Source={_dbPath}");
        SetEnvVar("AdminSeed__Enabled", "true");
        SetEnvVar("AdminSeed__User", "admin");
        SetEnvVar("AdminSeed__Password", "Adm!n-Test-12345");
        SetEnvVar("AdminSeed__LogGeneratedPassword", "false");
        SetEnvVar("Jwt__SigningKey", "test-signing-key-not-for-production-use-only-32b");
    }

    private void SetEnvVar(string name, string value)
    {
        // Remember whatever was there before so Dispose can put it back - these are
        // process-wide and must not leak into tests outside this fixture's lifetime.
        _replacedEnvVars.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            // Even if host disposal throws, the process-wide env vars must be restored
            // and the temp database cleaned up, or state leaks into subsequent tests.
            foreach ((string name, string? original) in _replacedEnvVars)
            {
                Environment.SetEnvironmentVariable(name, original);
            }

            // Sqlite connection pooling keeps the file handle open past host disposal.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch (IOException)
            {
                // Best effort - it's a uniquely named temp file either way.
            }
        }
    }
}
