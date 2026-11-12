using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[Authorize]
public class UserManagementController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .OrderBy(x => x.Email)
            .ToListAsync();

        var allRoles = await roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var result = new List<UserRoleViewModel>();
        foreach (var user in users)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            var model = new UserRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = userRoles.ToList()
            };

            model.AllRoles = allRoles.Select(x => new RoleSelection
            {
                RoleId = x.Id,
                RoleName = x.Name ?? string.Empty,
                IsSelected = userRoles.Contains(x.Name ?? string.Empty)
            }).ToList();

            result.Add(model);
        }

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string userId, string roleId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var role = await roleManager.FindByIdAsync(roleId);
        if (user is null || role is null)
            return NotFound();

        var isInRole = await userManager.IsInRoleAsync(user, role.Name!);
        if (!isInRole)
        {
            var result = await userManager.AddToRoleAsync(user, role.Name!);
            if (!result.Succeeded)
                return BadRequest(string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(string userId, string roleId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var role = await roleManager.FindByIdAsync(roleId);
        if (user is null || role is null)
            return NotFound();

        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Any(x => x.Id == user.Id) && adminUsers.Count == 1)
            {
                return BadRequest("Không thể gỡ quyền Admin khỏi tài khoản đang là quản trị viên chính.");
            }
        }

        var result = await userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(x => x.Description)));

        return RedirectToAction(nameof(Index));
    }

    public sealed class UserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
        public List<RoleSelection> AllRoles { get; set; } = [];
    }

    public sealed class RoleSelection
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
