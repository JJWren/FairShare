using System.Security.Claims;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Admin.Users;

[Authorize(Policy = "AdminOnly")]
[ValidateAntiForgeryToken]
public class DeleteModel(UserManager<ApplicationUser> userManager, ILogger<DeleteModel> logger) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<DeleteModel> _logger = logger;

    [BindProperty]
    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            return NotFound();
        }

        UserId = user.Id;
        UserName = user.UserName;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(UserId.ToString());

        if (user is null)
        {
            return NotFound();
        }

        // Prevent self-deletion
        Guid currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (user.Id == currentUserId)
        {
            ModelState.AddModelError(string.Empty, "You cannot delete your own account.");
            UserName = user.UserName;
            return Page();
        }

        string deletedUserName = user.UserName ?? user.Id.ToString();
        IdentityResult result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            foreach (IdentityError e in result.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }

            UserName = user.UserName;
            return Page();
        }

        _logger.LogWarning(
            "User {CurrentUser} deleted user {DeletedUser} (ID: {DeletedUserId})",
            User.Identity?.Name,
            deletedUserName,
            user.Id);

        TempData["SuccessMessage"] = $"User '{deletedUserName}' has been deleted.";
        return RedirectToPage("Index");
    }
}
