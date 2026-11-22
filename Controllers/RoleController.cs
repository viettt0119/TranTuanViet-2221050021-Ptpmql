using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Services;

namespace DemoMVC.Controllers;

[Authorize]
public class RoleController(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    PermissionCatalogService permissionCatalog) : Controller
{
    [HttpGet]
    [RequirePermission(DemoMVC.Services.Permissions.Role.View)]
    public async Task<IActionResult> Index()
    {
        var roles = await roleManager.Roles
            .OrderBy(x => x.Name)
            .ToListAsync();

        return View(roles);
    }

    [HttpGet]
    [RequirePermission(DemoMVC.Services.Permissions.Role.Create)]
    public IActionResult Create() => View(new IdentityRole());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(DemoMVC.Services.Permissions.Role.Create)]
    public async Task<IActionResult> Create(IdentityRole model)
    {
        model.Name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên role là bắt buộc.");
            return View(model);
        }
        if (await roleManager.RoleExistsAsync(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên vai trò đã tồn tại.");
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
    [RequirePermission(DemoMVC.Services.Permissions.Role.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(DemoMVC.Services.Permissions.Role.Edit)]
    public async Task<IActionResult> Edit(string id, IdentityRole model)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Không thể đổi tên role hệ thống Admin.";
            return RedirectToAction(nameof(Index));
        }

        model.Name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên role là bắt buộc.");
            return View(role);
        }
        var roleWithSameName = await roleManager.FindByNameAsync(model.Name);
        if (roleWithSameName is not null && roleWithSameName.Id != role.Id)
        {
            ModelState.AddModelError(nameof(model.Name), "Tên vai trò đã tồn tại.");
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
    [RequirePermission(DemoMVC.Services.Permissions.Role.Edit)]
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
            ModuleDisplayName = permission.ModuleDisplayName,
            Group = permission.Group,
            SortOrder = permission.SortOrder,
            IsGranted = granted.Contains(permission.Id)
        }).ToList();

        ViewBag.Role = role;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(DemoMVC.Services.Permissions.Role.Edit)]
    public async Task<IActionResult> Permissions(string id, string[]? selectedPermissions)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        var permissionIds = new HashSet<int>();
        foreach (var selectedPermission in selectedPermissions ?? [])
        {
            if (!int.TryParse(selectedPermission, out var permissionId))
            {
                ModelState.AddModelError(string.Empty, "Danh sách quyền không hợp lệ.");
                return RedirectToAction(nameof(Permissions), new { id });
            }
            permissionIds.Add(permissionId);
        }
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
        permissionCatalog.InvalidateAll();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(DemoMVC.Services.Permissions.Role.Delete)]
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
        public string ModuleDisplayName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsGranted { get; set; }
    }
}
