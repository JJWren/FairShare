using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using FairShare.Models;
using FairShare.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FairShare.Controllers
{
    [Authorize(Policy = "NotGuest")]
    public class SettingsController(UserManager<ApplicationUser> um, RoleManager<IdentityRole<Guid>> rm) : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager = um;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager = rm;

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Users(string filter = "all")
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

            return View(vm);
        }

        [Authorize(Policy = "AdminOnly")]
        public IActionResult CreateUser() => View(new CreateUserViewModel());

        [HttpPost, Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            Guid currentId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            ApplicationUser user = new ApplicationUser
            {
                UserName = model.UserName,
                CreatedUtc = DateTime.UtcNow,
                CreatedByUserId = currentId
            };

            IdentityResult result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (IdentityError e in result.Errors)
                {
                    ModelState.AddModelError("", e.Description);
                }

                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            return RedirectToAction(nameof(Users));
        }

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditUser(Guid id)
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return NotFound();
            }

            IList<string> roles = await _userManager.GetRolesAsync(user);

            return View(new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName!,
                Role = roles.FirstOrDefault() ?? "User",
                IsDisabled = user.IsDisabled
            });
        }

        [HttpPost, Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id.ToString());

            if (user is null)
            {
                return NotFound();
            }

            user.UserName = model.UserName;
            user.IsDisabled = model.IsDisabled;
            user.UpdatedUtc = DateTime.UtcNow;
            user.UpdatedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _userManager.UpdateAsync(user);

            IList<string> existingRoles = await _userManager.GetRolesAsync(user);

            if (!existingRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, existingRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost, Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            Guid me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (me == id)
            {
                return BadRequest("Cannot delete self.");
            }

            ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());

            if (user is null)
            {
                return NotFound();
            }

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(Users));
        }

        // General settings (theme etc.)
        public IActionResult Index()
        {
            return View(); // TODO: implement persistence later
        }
    }
}
