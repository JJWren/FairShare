using System.ComponentModel.DataAnnotations;

namespace FairShare.ViewModels;

public class RegisterViewModel
{
    [Required, MinLength(3), MaxLength(32)]
    [Display(Name = "User Name")]
    public string UserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool Allowed { get; set; } = true;
}
