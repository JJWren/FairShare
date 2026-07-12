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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    private const string RefreshCookieName = "fairshare_refresh";

    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly ITokenService _tokenService = tokenService;
    private readonly AuthOptions _authOptions = authOptions.Value;

    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<AuthConfigResponse> GetConfig() =>
        Ok(new AuthConfigResponse { AllowSelfRegistration = _authOptions.AllowSelfRegistration });

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        // No information leak: whether registration is open is public via GET config.
        if (!_authOptions.AllowSelfRegistration)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Self-registration is disabled on this server.");
        }

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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Guest(CancellationToken ct) => await IssueGuestTokensAsync(ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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

    [HttpPost("change-password")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ApplicationUser? user = userId is null ? null : await _userManager.FindByIdAsync(userId);

        if (user is null || user.IsDisabled)
        {
            return Unauthorized();
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        // Kill every other session, then re-issue for this one so the caller stays
        // signed in while stolen/old refresh tokens die immediately.
        await _tokenService.RevokeAllForUserAsync(user.Id, ct);

        return await IssueTokensAsync(user, ct);
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
    private void SetRefreshCookie(IssuedRefreshToken refresh) =>
        Response.Cookies.Append(RefreshCookieName, refresh.Value, BuildRefreshCookieOptions(refresh.ExpiresUtc));

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions());

    // SameSite=None requires Secure, which browsers only honor over HTTPS. Fall back to
    // Secure=false/SameSite=Lax when the request itself arrived over plain HTTP (e.g. the
    // "http" launch profile) so the cookie isn't silently dropped during local dev.
    // Set and Clear must always agree on these attributes, or the browser won't match the
    // cookie to delete it and a stale value is left behind.
    private CookieOptions BuildRefreshCookieOptions(DateTimeOffset? expires = null)
    {
        bool isHttps = Request.IsHttps;

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = expires,
            Path = "/api/v1/auth"
        };
    }
}
