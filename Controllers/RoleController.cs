using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[Authorize]
public class RoleController(RoleManager<IdentityRole> roleManager, ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await roleManager.Roles
            .OrderBy(x => x.Name)
            .ToListAsync();

        return View(roles);
    }

    [HttpGet]
    public IActionResult Create() => View(new IdentityRole());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IdentityRole model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên role là bắt buộc.");
            return View(model);
        }

        var result = await roleManager.CreateAsync(model);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, IdentityRole model)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên role là bắt buộc.");
            return View(role);
        }

        role.Name = model.Name;
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(role);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        var permissions = await dbContext.ApplicationPermissions
            .OrderBy(x => x.Controller)
            .ThenBy(x => x.Action)
            .ToListAsync();

        var granted = await dbContext.ApplicationRolePermissions
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.PermissionId)
            .ToListAsync();

        var model = permissions.Select(permission => new PermissionMatrixItem
        {
            PermissionId = permission.Id,
            PermissionKey = permission.Key,
            Name = permission.Description,
            IsGranted = granted.Contains(permission.Id)
        }).ToList();

        ViewBag.Role = role;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(string id, string[] selectedPermissions)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        var permissionIds = selectedPermissions.Select(int.Parse).ToHashSet();
        var allPermissions = await dbContext.ApplicationPermissions
            .ToListAsync();

        foreach (var permission in allPermissions)
        {
            var existing = await dbContext.ApplicationRolePermissions
                .FirstOrDefaultAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id);

            if (permissionIds.Contains(permission.Id))
            {
                if (existing is null)
                {
                    dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        IsGranted = true
                    });
                }
                else
                {
                    existing.IsGranted = true;
                }
            }
            else if (existing is not null)
            {
                existing.IsGranted = false;
            }
        }

        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Không thể xóa role hệ thống Admin.";
            return RedirectToAction(nameof(Index));
        }

        var hasUsers = await dbContext.UserRoles.AnyAsync(x => x.RoleId == role.Id);
        if (hasUsers)
        {
            TempData["ErrorMessage"] = "Không thể xóa role đang được gán cho người dùng.";
            return RedirectToAction(nameof(Index));
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = "Không thể xóa role.";
        }

        return RedirectToAction(nameof(Index));
    }

    public sealed class PermissionMatrixItem
    {
        public int PermissionId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
    }
}
