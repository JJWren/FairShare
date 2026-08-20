using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FairShare.Api.Auth;
using FairShare.Api.Models;
using FairShare.Api.Observability;
using FairShare.Api.Persistence;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace FairShare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    FairShareDbContext db,
    IConfiguration configuration,
    IAuditService audit,
    IAnalyticsService analytics) : ControllerBase
{
    private const string RefreshCookieName = "fairshare_refresh";

    /// <summary>Cookie scheme the Google handler signs its external principal into.</summary>
    public const string ExternalScheme = "External";
    public const string GoogleProvider = "Google";

    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly ITokenService _tokenService = tokenService;
    private readonly FairShareDbContext _db = db;
    private readonly IConfiguration _configuration = configuration;
    private readonly IAuditService _audit = audit;
    private readonly IAnalyticsService _analytics = analytics;

    private bool GoogleEnabled => !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]);

    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<AuthConfigResponse> GetConfig() =>
        Ok(new AuthConfigResponse { GoogleEnabled = GoogleEnabled });

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.FindByNameAsync(request.UserName);

        if (user is null || user.IsDisabled)
        {
            // The attempted username is an identifier, not case content; recording it is
            // what makes credential-stuffing visible in the audit view.
            await _audit.WriteAsync(AuditActions.LoginFailed, target: request.UserName, detail: user is null ? "unknown user" : "disabled", ct: ct);
            return Unauthorized();
        }

        Microsoft.AspNetCore.Identity.SignInResult signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!signIn.Succeeded)
        {
            await _audit.WriteAsync(AuditActions.LoginFailed, target: user.UserName, detail: signIn.IsLockedOut ? "locked out" : "bad password", ct: ct);
            return Unauthorized();
        }

        // TOTP gate (ADR 0004): enabled accounts (the admin's, per Q26) need a code on top
        // of the password. The 401 body distinguishes "code needed" from "bad credentials"
        // only AFTER the password verified - so it leaks nothing to a password guesser.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            string code = (request.TwoFactorCode ?? string.Empty).Replace(" ", string.Empty);

            if (code.Length == 0)
            {
                return Unauthorized(new TwoFactorRequiredResponse { RequiresTwoFactor = true });
            }

            bool codeValid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);

            if (!codeValid)
            {
                await _audit.WriteAsync(AuditActions.LoginFailed, target: user.UserName, detail: "bad totp", ct: ct);
                return Unauthorized(new TwoFactorRequiredResponse { RequiresTwoFactor = true, InvalidCode = true });
            }
        }

        await _audit.WriteAsync(AuditActions.LoginSucceeded, target: user.UserName, ct: ct);
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.SignIn, "local", ct);
        return await IssueTokensAsync(user, request.RememberDevice, ct);
    }

    // ---- Google (ADR 0004: the only public sign-up path) ---------------------------

    /// <summary>
    /// Starts the Google authorization-code flow. The API (not the SPA) drives it because
    /// the client secret lives here; the middleware handles state/correlation, lands the
    /// external principal in the External cookie, and returns to /google/complete.
    /// </summary>
    [HttpGet("google/start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public IActionResult GoogleStart(string? returnUrl, bool remember = false)
    {
        if (!GoogleEnabled)
        {
            return NotFound();
        }

        AuthenticationProperties props = new() { RedirectUri = "/api/v1/auth/google/complete" };
        props.Items["returnUrl"] = SafeReturnUrl(returnUrl);
        props.Items["remember"] = remember ? "1" : "0";

        return Challenge(props, GoogleProvider);
    }

    [HttpGet("google/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleComplete(CancellationToken ct)
    {
        if (!GoogleEnabled)
        {
            return NotFound();
        }

        AuthenticateResult external = await HttpContext.AuthenticateAsync(ExternalScheme);

        if (!external.Succeeded || external.Principal is null)
        {
            return Redirect(SpaUrl("/login", "error=google"));
        }

        string? subject = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = external.Principal.FindFirstValue(ClaimTypes.Email);
        string returnUrl = external.Properties?.Items.TryGetValue("returnUrl", out string? storedReturn) == true && storedReturn is not null
            ? storedReturn
            : "/";
        bool remember = external.Properties?.Items.TryGetValue("remember", out string? storedRemember) == true && storedRemember == "1";

        // The external cookie is single-purpose transport; drop it before anything can fail.
        await HttpContext.SignOutAsync(ExternalScheme);

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            return Redirect(SpaUrl("/login", "error=google"));
        }

        ApplicationUser? user = await _userManager.FindByLoginAsync(GoogleProvider, subject);

        if (user is null)
        {
            // We store only what sign-in needs: the subject (lookup key) and the email
            // (identity + default display name). Nothing else Google offers is kept (ADR 0004).
            user = new ApplicationUser
            {
                UserName = await UniqueUserNameFromEmailAsync(email),
                Email = email,
                CreatedUtc = DateTime.UtcNow
            };

            IdentityResult created = await _userManager.CreateAsync(user);

            if (!created.Succeeded)
            {
                return Redirect(SpaUrl("/login", "error=google"));
            }

            await _userManager.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, subject, GoogleProvider));
            await _userManager.AddToRoleAsync(user, "User");
            await _audit.WriteAsync(AuditActions.AccountCreated, target: user.UserName, detail: "google", ct: ct);
            await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.AccountCreated, "google", ct);
        }

        if (user.IsDisabled)
        {
            return Redirect(SpaUrl("/login", "error=disabled"));
        }

        await _audit.WriteAsync(AuditActions.LoginSucceeded, target: user.UserName, detail: "google", ct: ct);
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.SignIn, "google", ct);

        IssuedRefreshToken refresh = await _tokenService.IssueRefreshTokenAsync(user.Id, isGuest: false, persistent: remember, ct);
        SetRefreshCookie(refresh);

        // No token in the redirect: the SPA re-hydrates from the refresh cookie on load,
        // exactly like a page reload (signin=google just tells it to skip the guest fallback).
        return Redirect(SpaUrl(returnUrl, "signin=google"));
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

        // Rotation preserves the session's remember-this-device choice.
        return await IssueTokensAsync(user, existing.IsPersistent, ct);
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
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null || user.IsDisabled)
        {
            return Unauthorized();
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        // Read the current session's remember-this-device choice before the revoke below
        // erases the row - re-issuing must not silently upgrade a session-only sign-in.
        bool remember = await CurrentSessionPersistentAsync(ct);

        // Kill every other session, then re-issue for this one so the caller stays
        // signed in while stolen/old refresh tokens die immediately.
        await _tokenService.RevokeAllForUserAsync(user.Id, ct);
        await _audit.WriteAsync(AuditActions.PasswordChanged, target: user.UserName, ct: ct);

        return await IssueTokensAsync(user, remember, ct);
    }

    // ---- Account self-service (ADR 0004) -------------------------------------------

    [HttpPost("account/username")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> ChangeUserName([FromBody] ChangeUserNameRequest request, CancellationToken ct)
    {
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null || user.IsDisabled)
        {
            return Unauthorized();
        }

        string oldName = user.UserName ?? string.Empty;
        IdentityResult result = await _userManager.SetUserNameAsync(user, request.NewUserName);

        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _audit.WriteAsync(AuditActions.UserNameChanged, target: request.NewUserName, detail: $"was {oldName}", ct: ct);

        // The display name lives in the JWT, so hand back fresh tokens that carry it.
        return await IssueTokensAsync(user, await CurrentSessionPersistentAsync(ct), ct);
    }

    /// <summary>
    /// Hard delete: profiles, sessions, external logins, account - gone at once. "Leaving is
    /// as easy as arriving" is a launch requirement of public sign-up (ADR 0004). Audit rows
    /// naming the account survive until their own retention expires, disclosed on /privacy.
    /// </summary>
    [HttpDelete("account")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Confirm, "DELETE", StringComparison.Ordinal))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Type DELETE to confirm.");
        }

        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        // Audit before the delete so the actor identity is still resolvable.
        await _audit.WriteAsync(AuditActions.AccountDeleted, target: user.UserName, ct: ct);
        await _analytics.RecordEventAsync(HttpContext, AnalyticsEventNames.AccountDeleted, target: null, ct);

        await _db.ParentProfiles.Where(p => p.OwnerUserId == user.Id).ExecuteDeleteAsync(ct);
        await _db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);

        IdentityResult deleted = await _userManager.DeleteAsync(user);

        if (!deleted.Succeeded)
        {
            return IdentityValidationProblem(deleted);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    // ---- TOTP for local Admin accounts (ADR 0004, Q26) ------------------------------

    [HttpGet("2fa/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<TwoFactorStatusResponse>> TwoFactorStatus()
    {
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new TwoFactorStatusResponse { Enabled = await _userManager.GetTwoFactorEnabledAsync(user) });
    }

    [HttpGet("2fa/setup")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<TwoFactorSetupResponse>> TwoFactorSetup()
    {
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        string? key = await _userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = FormatKey(key!),
            AuthenticatorUri = $"otpauth://totp/FairShare:{Uri.EscapeDataString(user.UserName ?? "admin")}?secret={key}&issuer=FairShare&digits=6"
        });
    }

    [HttpPost("2fa/enable")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TwoFactorEnable([FromBody] TwoFactorCodeRequest request, CancellationToken ct)
    {
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        bool valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, request.Code.Replace(" ", string.Empty));

        if (!valid)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The code did not match. Check your authenticator app and try again.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        await _audit.WriteAsync(AuditActions.TwoFactorEnabled, target: user.UserName, ct: ct);
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TwoFactorDisable([FromBody] TwoFactorCodeRequest request, CancellationToken ct)
    {
        ApplicationUser? user = await GetCurrentUserAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        bool valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, request.Code.Replace(" ", string.Empty));

        if (!valid)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The code did not match. Check your authenticator app and try again.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _audit.WriteAsync(AuditActions.TwoFactorDisabled, target: user.UserName, ct: ct);
        return NoContent();
    }

    // ---- Helpers --------------------------------------------------------------------

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await _userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// The remember-this-device choice of the session presenting the refresh cookie.
    /// Defaults to persistent when no cookie is readable (e.g. a pure-bearer API client).
    /// </summary>
    private async Task<bool> CurrentSessionPersistentAsync(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

        bool? persistent = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (bool?)t.IsPersistent)
            .FirstOrDefaultAsync(ct);

        return persistent ?? true;
    }

    private async Task<string> UniqueUserNameFromEmailAsync(string email)
    {
        // Default display name = the email's local part, sanitized to Identity's allowed
        // characters; suffixed on collision. The user can change it in-app.
        string localPart = email.Split('@')[0];
        StringBuilder builder = new();

        foreach (char c in localPart)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_')
            {
                builder.Append(c);
            }
        }

        string baseName = builder.Length >= 3 ? builder.ToString() : "parent";

        string candidate = baseName;
        int suffix = 2;

        while (await _userManager.FindByNameAsync(candidate) is not null)
        {
            candidate = $"{baseName}-{suffix++}";
        }

        return candidate;
    }

    private static string FormatKey(string key)
    {
        // Groups of four ease manual entry into an authenticator app.
        StringBuilder formatted = new();

        for (int i = 0; i < key.Length; i += 4)
        {
            if (formatted.Length > 0)
            {
                formatted.Append(' ');
            }

            formatted.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return formatted.ToString().ToLowerInvariant();
    }

    /// <summary>Only same-origin absolute paths survive; anything else falls back to "/".</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";

    private string SpaUrl(string path, string query)
    {
        // The SPA lives on its own origin (standalone WASM behind nginx); the first CORS
        // origin is that origin. Dev fallback matches the dev compose's web port.
        string origin = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5858";

        string separator = path.Contains('?') ? "&" : "?";
        return $"{origin.TrimEnd('/')}{SafeReturnUrl(path)}{separator}{query}";
    }

    private async Task<IActionResult> IssueGuestTokensAsync(CancellationToken ct)
    {
        AccessToken access = _tokenService.IssueAccessToken(user: null, roles: [], isGuest: true);
        IssuedRefreshToken refresh = await _tokenService.IssueRefreshTokenAsync(userId: null, isGuest: true, persistent: true, ct);
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

    private async Task<IActionResult> IssueTokensAsync(ApplicationUser user, bool remember, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        AccessToken access = _tokenService.IssueAccessToken(user, roles.ToList(), isGuest: false);
        IssuedRefreshToken refresh = await _tokenService.IssueRefreshTokenAsync(user.Id, isGuest: false, persistent: remember, ct);
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
        Response.Cookies.Append(RefreshCookieName, refresh.Value,
            BuildRefreshCookieOptions(refresh.Persistent ? refresh.ExpiresUtc : null));

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions());

    private ActionResult IdentityValidationProblem(IdentityResult result) =>
        ValidationProblem(new ValidationProblemDetails(
            result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

    // SameSite=None requires Secure, which browsers only honor over HTTPS. Fall back to
    // Secure=false/SameSite=Lax when the request itself arrived over plain HTTP (e.g. the
    // "http" launch profile) so the cookie isn't silently dropped during local dev.
    // Set and Clear must always agree on these attributes, or the browser won't match the
    // cookie to delete it and a stale value is left behind.
    // A null expiry means a session cookie ("remember this device" unchecked).
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
