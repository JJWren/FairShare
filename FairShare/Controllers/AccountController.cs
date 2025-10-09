using FairShare.Models;
using FairShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FairShare.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<AccountController> logger,
    IOptions<AuthOptions> authOptions) : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn = signInManager;
    private readonly UserManager<ApplicationUser> _users = userManager;
    private readonly ILogger<AccountController> _logger = logger;
    private readonly AuthOptions _auth = authOptions.Value;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return Redirect("/");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string userName, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "User name and password required.");
            return View(model: returnUrl);
        }

        ApplicationUser? user = await _users.FindByNameAsync(userName);

        if (user is null || user.IsDisabled)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model: returnUrl);
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await _signIn.PasswordSignInAsync(user, password, false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {User} signed in.", userName);
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Invalid credentials.");
        return View(model: returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (!_auth.AllowSelfRegistration)
        {
            return NotFound();
        }

        RegisterViewModel vm = new() { Allowed = true };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!_auth.AllowSelfRegistration)
        {
            return NotFound();
        }

        vm.Allowed = true;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        ApplicationUser user = new ()
        {
            Id = Guid.NewGuid(),
            UserName = vm.UserName.Trim()
        };

        IdentityResult create = await _users.CreateAsync(user, vm.Password);

        if (!create.Succeeded)
        {
            foreach (var e in create.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }

            return View(vm);
        }

        // Default role for self-registered users (if allowed). Admins assign roles for others.
        if (!await _users.IsInRoleAsync(user, "User"))
        {
            await _users.AddToRoleAsync(user, "User");
        }

        TempData["Msg"] = "Account created. You may now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public async Task<IActionResult> Guest()
    {
        // Sign in using the primary Identity application cookie so global [Authorize] recognizes it.
        List<Claim> claims =
        [
            new (ClaimTypes.Name, "Guest"),
            new ("guest", "true")
        ];

        ClaimsIdentity identity = new (claims, IdentityConstants.ApplicationScheme);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
