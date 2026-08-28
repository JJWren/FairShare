using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using FairShare.Api.Models;
using FairShare.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FairShare.Api.Services;

public class AdminSeeder(
    IServiceProvider sp,
    ILogger<AdminSeeder> logger)
{
    private readonly IServiceProvider _sp = sp;
    private readonly ILogger<AdminSeeder> _logger = logger;

    public async Task SeedAsync()
    {
        using IServiceScope scope = _sp.CreateScope();

        // Load options
        AdminSeedOptions seedOpts = scope.ServiceProvider
            .GetRequiredService<IOptions<AdminSeedOptions>>().Value;

        if (!seedOpts.Enabled)
        {
            _logger.LogInformation("Admin seeding disabled via configuration.");
            return;
        }

        RoleManager<IdentityRole<Guid>> roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        string[] roles = ["Admin", "User"];

        foreach (string r in roles)
        {
            try
            {
                if (!await roleManager.RoleExistsAsync(r))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(r));
                }
            }
            catch (DbUpdateException)
            {
                // Lost a create race to another instance seeding the same database
                // concurrently; the role existing is all we need, so only rethrow if
                // it genuinely doesn't.
                if (!await roleManager.RoleExistsAsync(r))
                {
                    throw;
                }
            }
        }

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string adminUserName = seedOpts.User;
        ApplicationUser? admin = await userManager.FindByNameAsync(adminUserName);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminUserName,
                CreatedUtc = DateTime.UtcNow
            };

            bool generated = string.IsNullOrWhiteSpace(seedOpts.Password);
            string pwd = generated ? GenerateStrongPassword() : seedOpts.Password!;

            IdentityResult result = await userManager.CreateAsync(admin, pwd);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");

                if (generated)
                {
                    if (seedOpts.LogGeneratedPassword)
                    {
                        // Console.Error, deliberately NOT the logging pipeline: the SQLite
                        // sink persists log rows durably and serves them to admins, so the
                        // secret must never enter it. Operators read this via `docker logs`.
                        Console.Error.WriteLine($"[AdminSeeder] Seeded admin user '{adminUserName}' with generated password: {pwd}");
                        _logger.LogWarning("Seeded admin user '{User}' with a generated password (printed to stderr only).", adminUserName);
                    }
                    else
                    {
                        _logger.LogInformation("Seeded admin user '{User}' with a generated password (not logged).", adminUserName);
                    }
                }
                else
                {
                    _logger.LogInformation("Seeded admin user '{User}' with configured password.", adminUserName);
                }
            }
            else
            {
                _logger.LogError("Failed seeding admin '{User}': {Errors}",
                    adminUserName,
                    string.Join(';', result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }

    private static string GenerateStrongPassword()
    {
        // Uniform CSPRNG draw, redrawn until every character class is present so the
        // result always satisfies the Identity password policy. No fixed prefix or
        // format shape; confusable characters (I/l/O/0/1) are excluded because an
        // operator transcribes this once from the container output.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";

        while (true)
        {
            string candidate = RandomNumberGenerator.GetString(alphabet, 24);

            if (candidate.Any(char.IsUpper)
                && candidate.Any(char.IsLower)
                && candidate.Any(char.IsDigit)
                && candidate.Any(c => !char.IsLetterOrDigit(c)))
            {
                return candidate;
            }
        }
    }
}






