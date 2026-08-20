using System;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Contracts.Auth;

public class LoginRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Authenticator code, required only for accounts with TOTP enabled.</summary>
    public string? TwoFactorCode { get; set; }

    /// <summary>
    /// "Remember this device" (ADR 0004): false (the default) issues a session cookie that
    /// dies with the browser; true keeps the 30-day rotating refresh cookie.
    /// </summary>
    public bool RememberDevice { get; set; }
}

public class AuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresUtc { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsGuest { get; set; }
}

public class AuthConfigResponse
{
    /// <summary>Whether "Sign in with Google" is configured on this server.</summary>
    public bool GoogleEnabled { get; set; }
}

/// <summary>401 body when a password checked out but the account requires a TOTP code.
/// RequiresTwoFactor deliberately defaults to false so an empty/unrelated 401 body can
/// never deserialize into a spurious challenge.</summary>
public class TwoFactorRequiredResponse
{
    public bool RequiresTwoFactor { get; set; }
    public bool InvalidCode { get; set; }
}

public class TwoFactorSetupResponse
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
}

public class TwoFactorStatusResponse
{
    public bool Enabled { get; set; }
}

public class TwoFactorCodeRequest
{
    [Required, MinLength(6), MaxLength(8)]
    public string Code { get; set; } = string.Empty;
}

public class ChangeUserNameRequest
{
    [Required, MinLength(3), MaxLength(32)]
    public string NewUserName { get; set; } = string.Empty;
}

public class DeleteAccountRequest
{
    /// <summary>Must be the literal word DELETE - typed confirmation for a hard delete.</summary>
    [Required]
    public string Confirm { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
