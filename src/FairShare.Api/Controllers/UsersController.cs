using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using FairShare.Api.Auth;
using FairShare.Api.Models;
using FairShare.Api.Observability;
using FairShare.Api.Persistence;
using FairShare.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FairShare.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/v1/admin/users")]
public class UsersController(UserManager<ApplicationUser> um, RoleManager<IdentityRole<Guid>> rm, ITokenService tokenService, IAuditService audit, FairShareDbContext db) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = um;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager = rm;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IAuditService _audit = audit;
    private readonly FairShareDbContext _db = db;

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

        IdentityResult roleResult = await _userManager.AddToRoleAsync(user, model.Role);

        if (!roleResult.Succeeded)
        {
            // Creation is all-or-nothing: a user without their intended role is a
            // misconfigured account, so undo the create rather than report success.
            IdentityResult cleanup = await _userManager.DeleteAsync(user);

            if (!cleanup.Succeeded)
            {
                // Both halves failed - surface the stuck state rather than a plain 400
                // that implies nothing was created.
                return Problem(
                    statusCode: 500,
                    title: $"User '{user.UserName}' was created but role assignment failed, and cleanup also failed; delete the user manually.");
            }

            return BadRequest(roleResult.Errors);
        }

        await _audit.WriteAsync(AuditActions.UserCreated, target: user.UserName, detail: $"role {model.Role}");

        // A DTO, never the Identity entity: serializing ApplicationUser leaks
        // PasswordHash, SecurityStamp, and ConcurrencyStamp into the response body.
        UserListItem created = new()
        {
            Id = user.Id,
            UserName = user.UserName!,
            IsDisabled = user.IsDisabled,
            CreatedUtc = user.CreatedUtc,
            Role = model.Role
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, created);
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

        await _audit.WriteAsync(
            AuditActions.UserUpdated,
            target: user.UserName,
            detail: $"role {model.Role}{(model.IsDisabled ? ", disabled" : string.Empty)}");

        return NoContent();
    }

    // Self-reset is allowed on purpose: unlike self-delete it cannot lock the admin out
    // (they chose the new password), and killing their other sessions is the intended
    // semantics of a reset.
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, AdminResetPasswordRequest model, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        // Token-based reset keeps a single validation path and cannot strand the user
        // password-less the way RemovePassword+AddPassword can.
        string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        IdentityResult lockoutResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!lockoutResult.Succeeded)
        {
            return IdentityValidationProblem(lockoutResult);
        }

        user.UpdatedUtc = DateTime.UtcNow;
        user.UpdatedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        IdentityResult updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityValidationProblem(updateResult);
        }

        await _tokenService.RevokeAllForUserAsync(user.Id, ct);
        await _audit.WriteAsync(AuditActions.PasswordReset, target: user.UserName, ct: ct);

        return NoContent();
    }

    private ActionResult IdentityValidationProblem(IdentityResult result) =>
        ValidationProblem(new ValidationProblemDetails(
            result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        Guid me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (me == id) return BadRequest("Cannot delete self.");

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        // Same posture as self-delete: the account's saved rows go with it, in one
        // transaction. Relying on the FKs here left ownerless income rows behind -
        // ParentProfile's owner FK is SetNull for legacy reasons, and RefreshToken
        // has no FK at all.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.ParentProfiles.Where(p => p.OwnerUserId == user.Id).ExecuteDeleteAsync(ct);
        await _db.Scenarios.Where(s => s.OwnerUserId == user.Id).ExecuteDeleteAsync(ct);
        await _db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);

        IdentityResult deleted = await _userManager.DeleteAsync(user);

        if (!deleted.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(deleted.Errors);
        }

        await tx.CommitAsync(ct);

        await _audit.WriteAsync(AuditActions.UserDeleted, target: user.UserName, ct: ct);
        return NoContent();
    }
}
