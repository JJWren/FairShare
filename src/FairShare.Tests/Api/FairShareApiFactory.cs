using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FairShare.Tests.Api;

public class FairShareApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fairshare-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["AdminSeed:Enabled"] = "true",
                ["AdminSeed:User"] = "admin",
                ["AdminSeed:Password"] = "Adm!n-Test-12345",
                ["AdminSeed:LogGeneratedPassword"] = "false",
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production-use-only-32b"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
