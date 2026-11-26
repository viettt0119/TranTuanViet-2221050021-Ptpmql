using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DemoMVC.Controllers;

[Authorize]
[PermissionModule("Vai trò", "Hệ thống", 70)]
public class RoleController(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    PermissionCatalogService permissionCatalog) : Controller
{
    private const string AdminRole = "Admin";

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        var roles = roleManager.Roles.AsQueryable();
        var search = q?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var normalized = search.ToUpperInvariant();
            roles = roles.Where(role => role.NormalizedName!.Contains(normalized));
        }

        var rows = roles
            .OrderBy(role => role.Name)
            .Select(role => new RoleRow
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = dbContext.UserRoles.Count(userRole => userRole.RoleId == role.Id),
                GrantedCount = dbContext.ApplicationRolePermissions.Count(grant => grant.RoleId == role.Id && grant.IsGranted)
            });

        ViewData["SearchQuery"] = search;
        ViewData["TotalPermissions"] = await dbContext.ApplicationPermissions.CountAsync();
        return View(await PagedList<RoleRow>.CreateAsync(rows, page, pageSize));
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        var users = await dbContext.UserRoles
            .Where(userRole => userRole.RoleId == role.Id)
            .Join(dbContext.Users, userRole => userRole.UserId, user => user.Id, (_, user) => user)
            .OrderBy(user => user.Email)
            .Select(user => new RoleUserItem { Email = user.Email ?? string.Empty, FullName = user.FullName })
            .ToListAsync();

        var permissions = await dbContext.ApplicationRolePermissions
            .Where(grant => grant.RoleId == role.Id && grant.IsGranted)
            .Select(grant => grant.Permission)
            .OrderBy(permission => permission.SortOrder)
            .ThenBy(permission => permission.Key)
            .ToListAsync();

        return PartialView("_DetailsModal", new RoleDetailsViewModel
        {
            Role = role,
            Users = users,
            Permissions = permissions,
            IsSystemAdmin = IsAdminRole(role)
        });
    }

    [HttpGet]
    public IActionResult Create() =>
        FormModal(new RoleFormViewModel(), isCreate: true, statusCode: StatusCodes.Status200OK);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel form)
    {
        form.Name = form.Name?.Trim() ?? string.Empty;
        if (ModelState.IsValid && await roleManager.RoleExistsAsync(form.Name))
            ModelState.AddModelError(nameof(form.Name), "Tên vai trò đã tồn tại.");
        if (!ModelState.IsValid)
            return FormModal(form, isCreate: true);

        var result = await roleManager.CreateAsync(new IdentityRole(form.Name));
        if (!result.Succeeded)
        {
            AddErrors(result);
            return FormModal(form, isCreate: true);
        }

        return Json(new { success = true, message = $"Đã thêm vai trò {form.Name}." });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (SystemRoles.IsSystemRole(role.Name))
            return Forbidden($"Không thể đổi tên vai trò hệ thống {role.Name}.");

        return FormModal(new RoleFormViewModel { Id = role.Id, Name = role.Name ?? string.Empty },
            isCreate: false, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, RoleFormViewModel form)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (SystemRoles.IsSystemRole(role.Name))
            return Forbidden($"Không thể đổi tên vai trò hệ thống {role.Name}.");
        if (await IsProtectedFromNonAdminAsync(role))
            return Forbidden("Bạn không thể sửa vai trò mà chính mình đang có.");

        form.Id = id;
        form.Name = form.Name?.Trim() ?? string.Empty;
        if (ModelState.IsValid)
        {
            var sameName = await roleManager.FindByNameAsync(form.Name);
            if (sameName is not null && sameName.Id != role.Id)
                ModelState.AddModelError(nameof(form.Name), "Tên vai trò đã tồn tại.");
            if (SystemRoles.IsSystemRole(form.Name))
                ModelState.AddModelError(nameof(form.Name), $"Tên {form.Name} được dành cho vai trò hệ thống.");
        }
        if (!ModelState.IsValid)
            return FormModal(form, isCreate: false);

        role.Name = form.Name;
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return FormModal(form, isCreate: false);
        }

        permissionCatalog.InvalidateAll();
        return Json(new { success = true, message = $"Đã đổi tên vai trò thành {form.Name}." });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (SystemRoles.IsSystemRole(role.Name))
            return Forbidden($"Không thể xoá vai trò hệ thống {role.Name}.");

        ViewData["UserCount"] = await dbContext.UserRoles.CountAsync(x => x.RoleId == role.Id);
        return PartialView("_DeleteModal", role);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (SystemRoles.IsSystemRole(role.Name))
            return Forbidden($"Không thể xoá vai trò hệ thống {role.Name}.");
        if (await IsProtectedFromNonAdminAsync(role))
            return Forbidden("Bạn không thể xoá vai trò mà chính mình đang có.");

        var userCount = await dbContext.UserRoles.CountAsync(x => x.RoleId == role.Id);
        if (userCount > 0)
        {
            ModelState.AddModelError(string.Empty, "Không thể xoá vai trò đang được gán cho người dùng. Hãy gỡ vai trò khỏi người dùng trước.");
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            ViewData["UserCount"] = userCount;
            return PartialView("_DeleteModal", role);
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            AddErrors(result);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            ViewData["UserCount"] = userCount;
            return PartialView("_DeleteModal", role);
        }

        permissionCatalog.InvalidateAll();
        return Json(new { success = true, message = $"Đã xoá vai trò {role.Name}." });
    }

    [HttpGet]
    [PermissionAction("Phân quyền cho vai trò")]
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        return PartialView("_PermissionsModal", await BuildPermissionMatrixAsync(role));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(string id, int[]? selectedPermissions)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (IsAdminRole(role))
            return Forbidden("Vai trò Admin luôn có toàn bộ quyền, không cần phân quyền.");
        if (await IsProtectedFromNonAdminAsync(role))
            return Forbidden("Bạn không thể tự phân quyền cho vai trò mà chính mình đang có.");

        var selected = (selectedPermissions ?? []).ToHashSet();
        var allPermissions = await dbContext.ApplicationPermissions.ToListAsync();
        var existingGrants = await dbContext.ApplicationRolePermissions
            .Where(x => x.RoleId == role.Id)
            .ToDictionaryAsync(x => x.PermissionId);
        // Người không phải Admin chỉ được bật/tắt những quyền chính mình đang có (chống leo thang quyền).
        var editable = await GetEditablePermissionKeysAsync();

        foreach (var permission in allPermissions)
        {
            if (editable is not null && !editable.Contains(permission.Key))
                continue;

            var grant = selected.Contains(permission.Id);
            if (existingGrants.TryGetValue(permission.Id, out var existing))
            {
                existing.IsGranted = grant;
            }
            else if (grant)
            {
                dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    IsGranted = true
                });
            }
        }

        await dbContext.SaveChangesAsync();
        permissionCatalog.InvalidateAll();
        return Json(new { success = true, message = $"Đã cập nhật quyền cho vai trò {role.Name}." });
    }

    private async Task<RolePermissionsViewModel> BuildPermissionMatrixAsync(IdentityRole role)
    {
        var permissions = await dbContext.ApplicationPermissions
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Controller)
            .ThenBy(x => x.Action)
            .ToListAsync();
        var granted = (await dbContext.ApplicationRolePermissions
            .Where(x => x.RoleId == role.Id && x.IsGranted)
            .Select(x => x.PermissionId)
            .ToListAsync()).ToHashSet();
        var editable = await GetEditablePermissionKeysAsync();
        var isAdminRole = IsAdminRole(role);
        var isOwnRole = await IsProtectedFromNonAdminAsync(role);

        return new RolePermissionsViewModel
        {
            Role = role,
            ReadOnlyReason = isAdminRole
                ? "Vai trò Admin luôn có toàn bộ quyền."
                : isOwnRole ? "Bạn không thể tự phân quyền cho vai trò mà chính mình đang có." : null,
            Items = permissions.Select(permission => new PermissionMatrixItem
            {
                PermissionId = permission.Id,
                PermissionKey = permission.Key,
                ModuleDisplayName = permission.ModuleDisplayName,
                ActionDisplayName = permission.ActionDisplayName,
                Group = permission.Group,
                SortOrder = permission.SortOrder,
                IsGranted = isAdminRole || granted.Contains(permission.Id),
                CanEdit = !isAdminRole && !isOwnRole && (editable is null || editable.Contains(permission.Key))
            }).ToList()
        };
    }

    // null = được sửa mọi quyền (Admin).
    private async Task<HashSet<string>?> GetEditablePermissionKeysAsync() =>
        User.IsInRole(AdminRole) ? null : await permissionCatalog.GetGrantedKeysAsync(User);

    private static bool IsAdminRole(IdentityRole role) =>
        string.Equals(role.Name, AdminRole, StringComparison.OrdinalIgnoreCase);

    // Người không phải Admin không được sửa/xoá/phân quyền vai trò của chính mình (tự cấp thêm quyền).
    // Đọc từ CSDL thay vì cookie: cookie chỉ được cấp lại role mới sau tối đa 1 phút.
    private async Task<bool> IsProtectedFromNonAdminAsync(IdentityRole role)
    {
        if (User.IsInRole(AdminRole))
            return false;
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userId is not null &&
            await dbContext.UserRoles.AnyAsync(userRole => userRole.UserId == userId && userRole.RoleId == role.Id);
    }

    private PartialViewResult FormModal(RoleFormViewModel form, bool isCreate, int statusCode = StatusCodes.Status422UnprocessableEntity)
    {
        Response.StatusCode = statusCode;
        ViewData["IsCreate"] = isCreate;
        ViewData["FormActionUrl"] = isCreate ? Url.Action(nameof(Create)) : Url.Action(nameof(Edit), new { id = form.Id });
        return PartialView("_FormModal", form);
    }

    private JsonResult Forbidden(string message) =>
        new(new { success = false, message }) { StatusCode = StatusCodes.Status403Forbidden };

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    public sealed class RoleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int GrantedCount { get; set; }
    }

    public sealed class RoleFormViewModel
    {
        public string? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Tên vai trò là bắt buộc.")]
        [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2, ErrorMessage = "Tên vai trò dài từ 2 đến 50 ký tự.")]
        [System.ComponentModel.DataAnnotations.RegularExpression(@"^[\p{L}0-9 _.-]+$", ErrorMessage = "Tên vai trò chỉ gồm chữ, số, khoảng trắng và . _ -")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Tên vai trò")]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class RoleUserItem
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class RoleDetailsViewModel
    {
        public IdentityRole Role { get; set; } = null!;
        public List<RoleUserItem> Users { get; set; } = [];
        public List<ApplicationPermission> Permissions { get; set; } = [];
        public bool IsSystemAdmin { get; set; }
    }

    public sealed class RolePermissionsViewModel
    {
        public IdentityRole Role { get; set; } = null!;
        public string? ReadOnlyReason { get; set; }
        public List<PermissionMatrixItem> Items { get; set; } = [];
    }

    public sealed class PermissionMatrixItem
    {
        public int PermissionId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string ModuleDisplayName { get; set; } = string.Empty;
        public string ActionDisplayName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsGranted { get; set; }
        public bool CanEdit { get; set; }
    }
}
