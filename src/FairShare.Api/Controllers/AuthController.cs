using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Auth;
using FairShare.Api.Models;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService) : ControllerBase
{
    private const string RefreshCookieName = "fairshare_refresh";

    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly ITokenService _tokenService = tokenService;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        ApplicationUser user = new()
        {
            UserName = request.UserName,
            CreatedUtc = DateTime.UtcNow
        };

        IdentityResult result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        await _userManager.AddToRoleAsync(user, "User");

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.FindByNameAsync(request.UserName);

        if (user is null || user.IsDisabled)
        {
            return Unauthorized();
        }

        Microsoft.AspNetCore.Identity.SignInResult signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!signIn.Succeeded)
        {
            return Unauthorized();
        }

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("guest")]
    [AllowAnonymous]
    public async Task<IActionResult> Guest(CancellationToken ct) => await IssueGuestTokensAsync(ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out string? rawRefreshToken) || string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized();
        }

        RefreshToken? existing = await _tokenService.ConsumeRefreshTokenAsync(rawRefreshToken, ct);

        if (existing is null)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        if (existing.IsGuest)
        {
            return await IssueGuestTokensAsync(ct);
        }

        ApplicationUser? user = existing.UserId is null
            ? null
            : await _userManager.FindByIdAsync(existing.UserId.ToString()!);

        if (user is null || user.IsDisabled)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out string? rawRefreshToken) && !string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await _tokenService.ConsumeRefreshTokenAsync(rawRefreshToken, ct);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    private async Task<IActionResult> IssueGuestTokensAsync(CancellationToken ct)
    {
        AccessToken access = _tokenService.IssueAccessToken(user: null, roles: [], isGuest: true);
        IssuedRefreshToken refresh = await _tokenService.IssueRefreshTokenAsync(userId: null, isGuest: true, ct);
        SetRefreshCookie(refresh);

        return Ok(new AuthTokenResponse
        {
            AccessToken = access.Value,
            AccessTokenExpiresUtc = access.ExpiresUtc,
            UserName = "Guest",
            Role = string.Empty,
            IsGuest = true
        });
    }

    private async Task<IActionResult> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        AccessToken access = _tokenService.IssueAccessToken(user, roles.ToList(), isGuest: false);
        IssuedRefreshToken refresh = await _tokenService.IssueRefreshTokenAsync(user.Id, isGuest: false, ct);
        SetRefreshCookie(refresh);

        return Ok(new AuthTokenResponse
        {
            AccessToken = access.Value,
            AccessTokenExpiresUtc = access.ExpiresUtc,
            UserName = user.UserName ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "User",
            IsGuest = false
        });
    }

    // Scoped to the auth path so the cookie is never sent to (or exposed via) any other API route.
    private void SetRefreshCookie(IssuedRefreshToken refresh)
    {
        Response.Cookies.Append(RefreshCookieName, refresh.Value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = refresh.ExpiresUtc,
            Path = "/api/v1/auth"
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/api/v1/auth"
        });
    }
}
