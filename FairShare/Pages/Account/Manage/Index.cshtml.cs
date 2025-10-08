using System.ComponentModel.DataAnnotations;
using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Account.Manage;

[Authorize]
public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public string? Status { get; set; }

    public class ProfileInput
    {
        [Display(Name = "Display Name")]
        [StringLength(64)]
        public string? DisplayName { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        Input.DisplayName = user.DisplayName;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!ModelState.IsValid) return Page();

        user.DisplayName = Input.DisplayName;
        await _userManager.UpdateAsync(user);
        Status = "Saved.";
        return Page();
    }
}
