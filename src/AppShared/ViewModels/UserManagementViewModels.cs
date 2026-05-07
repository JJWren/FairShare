using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;

namespace FairShare.AppShared.ViewModels
{
    public class UserListItemViewModel
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = "";

        public string? Role { get; set; }

        public bool IsDisabled { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }

        public DateTime? LastSeenUtc { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required, MinLength(3)]
        public string UserName { get; set; } = "";

        [Required, MinLength(8)]
        public string Password { get; set; } = "";

        [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public string Role { get; set; } = "User";
    }

    public class EditUserViewModel
    {
        public Guid Id { get; set; }

        [Required, MinLength(3)]
        public string UserName { get; set; } = "";

        [Required]
        public string Role { get; set; } = "User";

        public bool IsDisabled { get; set; }
    }
}






