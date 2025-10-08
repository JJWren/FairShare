using System.Security.Cryptography;
using FairShare.Models;
using FairShare.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FairShare.Services;

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
            if (!await roleManager.RoleExistsAsync(r))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(r));
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
                        _logger.LogWarning("Seeded admin user '{User}' with generated password: {Pwd}", adminUserName, pwd);
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
        return $"Adm!n{Guid.NewGuid():N}".Substring(0, 16);
    }
}
