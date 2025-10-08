using System.ComponentModel.DataAnnotations;
using FairShare.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairShare.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _config = config;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public bool AllowRegistration { get; private set; }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public class RegisterInput
    {
        [Required, StringLength(32)]
        public string UserName { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(64, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        AllowRegistration = _config.GetValue<bool>("Auth:AllowSelfRegistration");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AllowRegistration = _config.GetValue<bool>("Auth:AllowSelfRegistration");

        if (!AllowRegistration)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = new ApplicationUser
        {
            UserName = Input.UserName,
            CreatedUtc = DateTime.UtcNow
        };

        IdentityResult result = await _userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("User '{User}' self-registered.", user.UserName);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToPage("/Index");
        }

        foreach (IdentityError e in result.Errors)
        {
            ModelState.AddModelError(string.Empty, e.Description);
        }

        return Page();
    }
}
