using System;
using System.ComponentModel.DataAnnotations;

namespace FairShare.Contracts.Admin;

public class UserListItem
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}

public class CreateUserRequest
{
    [Required, MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "User";
}

public class AdminResetPasswordRequest
{
    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class EditUserRequest
{
    public Guid Id { get; set; }

    [Required, MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "User";

    public bool IsDisabled { get; set; }
}
