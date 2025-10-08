using System.ComponentModel.DataAnnotations;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Admin.Users;

[Authorize(Policy = "AdminOnly")]
public class EditModel(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<EditModel> logger) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager = roleManager;
    private readonly ILogger<EditModel> _logger = logger;

    [BindProperty]
    public EditInput Input { get; set; } = new();

    [BindProperty]
    public List<RoleChoice> RoleChoices { get; set; } = [];

    public string? StatusMessage { get; set; }

    public class EditInput
    {
        public Guid Id { get; set; }

        [Required, StringLength(32)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(64)]
        public string? DisplayName { get; set; }
    }

    public class RoleChoice
    {
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return NotFound();
        }

        Input = new EditInput
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName
        };

        await LoadRoleChoicesAsync(user);
        return Page();
    }

    private async Task LoadRoleChoicesAsync(ApplicationUser user)
    {
        IList<string> userRoles = await _userManager.GetRolesAsync(user);
        RoleChoices = _roleManager.Roles
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .Select(n => new RoleChoice
            {
                Name = n,
                Selected = userRoles.Contains(n)
            })
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(Input.Id.ToString());

        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadRoleChoicesAsync(user);
            return Page();
        }

        // Validate only one role is selected
        int selectedCount = RoleChoices.Count(r => r.Selected);
        if (selectedCount == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select at least one role.");
            await LoadRoleChoicesAsync(user);
            return Page();
        }
        if (selectedCount > 1)
        {
            ModelState.AddModelError(string.Empty, "A user can only have one role.");
            await LoadRoleChoicesAsync(user);
            return Page();
        }

        user.UserName = Input.UserName;
        user.DisplayName = Input.DisplayName;

        IdentityResult updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            foreach (IdentityError e in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }

            await LoadRoleChoicesAsync(user);
            return Page();
        }

        // Roles
        string[] selected = RoleChoices.Where(r => r.Selected).Select(r => r.Name).ToArray();
        IList<string> current = await _userManager.GetRolesAsync(user);

        string[] toRemove = current.Where(r => !selected.Contains(r)).ToArray();
        string[] toAdd = selected.Where(r => !current.Contains(r)).ToArray();

        if (toRemove.Length > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, toRemove);
        }

        if (toAdd.Length > 0)
        {
            await _userManager.AddToRolesAsync(user, toAdd);
        }

        StatusMessage = "Saved.";
        return RedirectToPage(new { id = user.Id });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid id, [Required, StringLength(100, MinimumLength = 8)] string newPassword)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ModelState.AddModelError("newPassword", "Password is required.");
            Input = new EditInput
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                DisplayName = user.DisplayName
            };
            await LoadRoleChoicesAsync(user);
            return Page();
        }

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult reset = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (!reset.Succeeded)
        {
            Input = new EditInput
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                DisplayName = user.DisplayName
            };
            await LoadRoleChoicesAsync(user);

            foreach (var e in reset.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }

            return Page();
        }

        StatusMessage = "Password has been reset successfully.";
        Input = new EditInput
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName
        };
        await LoadRoleChoicesAsync(user);
        return Page();
    }
}
