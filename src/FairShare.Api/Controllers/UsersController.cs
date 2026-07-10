using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Models;
using FairShare.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/v1/admin/users")]
public class UsersController(UserManager<ApplicationUser> um, RoleManager<IdentityRole<Guid>> rm) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = um;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager = rm;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserListItem>>> GetUsers(string filter = "all")
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
        var items = new List<UserListItem>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItem
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

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EditUserRequest>> GetUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new EditUserRequest
        {
            Id = user.Id,
            UserName = user.UserName!,
            Role = roles.FirstOrDefault() ?? "User",
            IsDisabled = user.IsDisabled
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest model)
    {
        Guid currentId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        ApplicationUser user = new()
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, EditUserRequest model)
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
