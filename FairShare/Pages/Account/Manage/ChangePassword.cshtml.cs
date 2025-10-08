using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Account.Manage;

[Authorize(Policy = "NotGuest")]
public class ChangePasswordModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<ChangePasswordModel> logger) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly ILogger<ChangePasswordModel> _logger = logger;

    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public class ChangePasswordInput
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The {0} must be at least {2} characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        // Block demo and guest users
        if (user.UserName == "demo" || User.HasClaim(c => c.Type == "guest" && c.Value == "true"))
        {
            return Forbid();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge();
        }

        // Block demo and guest users
        if (user.UserName == "demo" || User.HasClaim(c => c.Type == "guest" && c.Value == "true"))
        {
            return Forbid();
        }

        IdentityResult changePasswordResult = await _userManager.ChangePasswordAsync(
            user,
            Input.CurrentPassword,
            Input.NewPassword);

        if (!changePasswordResult.Succeeded)
        {
            foreach (IdentityError error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("User {UserId} changed their password successfully.", user.Id);

        StatusMessage = "Your password has been changed successfully.";
        return RedirectToPage();
    }
}
