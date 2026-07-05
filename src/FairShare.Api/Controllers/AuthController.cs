using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Auth;
using FairShare.Api.Models;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
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
                result.Errors.ToLookup(_ => "Password", e => e.Description)
                    .ToDictionary(g => g.Key, g => g.ToArray())));
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
    public async Task<IActionResult> Guest(CancellationToken ct)
    {
        AccessToken access = _tokenService.IssueAccessToken(user: null, roles: [], isGuest: true);
        string refresh = await _tokenService.IssueRefreshTokenAsync(userId: null, isGuest: true, ct);

        return Ok(new AuthTokenResponse
        {
            AccessToken = access.Value,
            AccessTokenExpiresUtc = access.ExpiresUtc,
            RefreshToken = refresh,
            UserName = "Guest",
            Role = string.Empty,
            IsGuest = true
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        Models.RefreshToken? existing = await _tokenService.ConsumeRefreshTokenAsync(request.RefreshToken, ct);

        if (existing is null)
        {
            return Unauthorized();
        }

        if (existing.IsGuest)
        {
            AccessToken access = _tokenService.IssueAccessToken(user: null, roles: [], isGuest: true);
            string refresh = await _tokenService.IssueRefreshTokenAsync(userId: null, isGuest: true, ct);

            return Ok(new AuthTokenResponse
            {
                AccessToken = access.Value,
                AccessTokenExpiresUtc = access.ExpiresUtc,
                RefreshToken = refresh,
                UserName = "Guest",
                Role = string.Empty,
                IsGuest = true
            });
        }

        ApplicationUser? user = existing.UserId is null
            ? null
            : await _userManager.FindByIdAsync(existing.UserId.ToString()!);

        if (user is null || user.IsDisabled)
        {
            return Unauthorized();
        }

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _tokenService.ConsumeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    private async Task<IActionResult> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        AccessToken access = _tokenService.IssueAccessToken(user, roles.ToList(), isGuest: false);
        string refresh = await _tokenService.IssueRefreshTokenAsync(user.Id, isGuest: false, ct);

        return Ok(new AuthTokenResponse
        {
            AccessToken = access.Value,
            AccessTokenExpiresUtc = access.ExpiresUtc,
            RefreshToken = refresh,
            UserName = user.UserName ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "User",
            IsGuest = false
        });
    }
}
