using FairShare.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;

namespace FairShare.Pages.Admin.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public List<UserRow> Users { get; private set; } = [];

    public class UserRow
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string[] Roles { get; set; } = [];
        public DateTime? CreatedUtc { get; set; }
    }

    public async Task OnGetAsync()
    {
        List<ApplicationUser> all = [.. _userManager.Users];

        foreach (ApplicationUser u in all)
        {
            IList<string> roles = await _userManager.GetRolesAsync(u);
            Users.Add(new UserRow
            {
                Id = u.Id,
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                Roles = roles.ToArray(),
                CreatedUtc = u.CreatedUtc
            });
        }
    }
}
