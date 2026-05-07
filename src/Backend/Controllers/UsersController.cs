using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Shared.Models;
using FairShare.Shared.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Backend.Controllers;

/// <summary>
/// Provides administrative actions for user management, including listing, creating, updating, and deleting users.
/// </summary>
/// <param name="um">The <see cref="UserManager{TUser}"/> for handling user operations.</param>
/// <param name="rm">The <see cref="RoleManager{TRole}"/> for handling role assignments.</param>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/[controller]")]
public class UsersController(UserManager<ApplicationUser> um, RoleManager<IdentityRole<Guid>> rm) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = um;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager = rm;

    /// <summary>
    /// Retrieves a list of users filtered by their status (all, enabled, or disabled).
    /// </summary>
    /// <param name="filter">The status filter to apply.</param>
    /// <returns>A list of <see cref="UserListItemViewModel"/> representing the matching users.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserListItemViewModel>>> GetUsers(string filter = "all")
    {
        IQueryable<ApplicationUser> usersQuery = _userManager.Users.AsQueryable();
        filter = filter.ToLowerInvariant();
        usersQuery = filter switch
        {
            "enabled" => usersQuery.Where(u => !u.IsDisabled),
            "disabled" => usersQuery.Where(u => u.IsDisabled),
            _ => usersQuery
        };

        var users = usersQuery.ToList();
        var vm = new List<UserListItemViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            vm.Add(new UserListItemViewModel
            {
                Id = u.Id,
                UserName = u.UserName!,
                IsDisabled = u.IsDisabled,
                CreatedUtc = u.CreatedUtc,
                LastSeenUtc = u.LastSeenUtc,
                UpdatedUtc = u.UpdatedUtc,
                Role = roles.FirstOrDefault() ?? "User"
            });
        }

        return Ok(vm);
    }

    /// <summary>
    /// Retrieves the details of a specific user for editing.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <returns>An <see cref="EditUserViewModel"/> for the requested user.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<EditUserViewModel>> GetUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName!,
            Role = roles.FirstOrDefault() ?? "User",
            IsDisabled = user.IsDisabled
        });
    }

    /// <summary>
    /// Creates a new application user with the specified credentials and role.
    /// </summary>
    /// <param name="model">The creation details for the new user.</param>
    /// <returns>The created user's data or an error result.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        Guid currentId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        ApplicationUser user = new ApplicationUser
        {
            UserName = model.UserName,
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = currentId
        };

        IdentityResult result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, model.Role);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    /// <summary>
    /// Updates the profile and status of an existing user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="model">The updated user details.</param>
    /// <returns>A no-content result on success.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, EditUserViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.UserName = model.UserName;
        user.IsDisabled = model.IsDisabled;
        user.UpdatedUtc = DateTime.UtcNow;
        user.UpdatedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        IList<string> existingRoles = await _userManager.GetRolesAsync(user);
        if (!existingRoles.Contains(model.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a user from the system. Prevents users from deleting themselves.
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <returns>A no-content result on success.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        Guid me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (me == id) return BadRequest("Cannot delete self.");

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        await _userManager.DeleteAsync(user);
        return NoContent();
    }
}





