using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

public class FairShareApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fairshare-tests-{Guid.NewGuid():N}.db");

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
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={_dbPath}");
        Environment.SetEnvironmentVariable("AdminSeed__Enabled", "true");
        Environment.SetEnvironmentVariable("AdminSeed__User", "admin");
        Environment.SetEnvironmentVariable("AdminSeed__Password", "Adm!n-Test-12345");
        Environment.SetEnvironmentVariable("AdminSeed__LogGeneratedPassword", "false");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-not-for-production-use-only-32b");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

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
