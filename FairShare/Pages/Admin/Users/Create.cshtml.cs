using System.ComponentModel.DataAnnotations;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Admin.Users;

[Authorize(Policy = "AdminOnly")]
public class CreateModel(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<CreateModel> logger) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager = roleManager;
    private readonly ILogger<CreateModel> _logger = logger;

    [BindProperty]
    public CreateInput Input { get; set; } = new();

    [BindProperty]
    public List<RoleChoice> RoleChoices { get; set; } = [];

    public class CreateInput
    {
        [Required, StringLength(32)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(64)]
        public string? DisplayName { get; set; }

        [Required, DataType(DataType.Password), StringLength(64, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }

    public class RoleChoice
    {
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public void OnGet()
    {
        RoleChoices = _roleManager.Roles
            .Select(r => new RoleChoice { Name = r.Name! })
            .OrderBy(r => r.Name)
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        RoleChoices = _roleManager.Roles
            .Select(r => new RoleChoice { Name = r.Name! })
            .OrderBy(r => r.Name)
            .ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = new ApplicationUser
        {
            UserName = Input.UserName,
            DisplayName = Input.DisplayName,
            CreatedUtc = DateTime.UtcNow
        };

        IdentityResult result = await _userManager.CreateAsync(user, Input.Password);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }

            return Page();
        }

        string[] selectedRoles = RoleChoices.Where(r => r.Selected).Select(r => r.Name).ToArray();

        if (selectedRoles.Length > 0)
        {
            IdentityResult addRolesResult = await _userManager.AddToRolesAsync(user, selectedRoles);

            if (!addRolesResult.Succeeded)
            {
                foreach (var e in addRolesResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }
        }

        _logger.LogInformation("Admin created user {User}", user.UserName);
        return RedirectToPage("Index");
    }
}
