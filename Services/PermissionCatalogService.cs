using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DemoMVC.Services;

public sealed class PermissionCatalogService(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    IMemoryCache memoryCache)
{
    // Quyền mặc định của role User khi role này chưa có quyền nào (chỉ xem và các trang bài học).
    public static readonly IReadOnlyList<string> DefaultUserPermissionKeys =
    [
        "Student.Index", "Student.Details",
        "Department.Index", "Department.Details",
        "Employee.Index", "Employee.Details",
        "Product.Index", "Product.Details",
        "Lec5.Index", "Lec5.Details",
        "Lesson.Lesson1", "Lesson.Lesson2", "Lesson.Lesson3",
        "Lesson.Lesson4", "Lesson.Lesson5", "Lesson.Lesson6",
        "Lesson4.Index", "Lesson4.Greet", "Lesson4.ProductById"
    ];

    // Hệ thống cũ phân quyền theo module (vd. "Student.View"). Khi nâng cấp, quyền đã cấp được chuyển
    // sang các action tương ứng để role không bị mất quyền.
    private static readonly Dictionary<string, string[]> LegacyActionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["View"] = ["Index", "Details", "DownloadTemplate"],
        ["Create"] = ["Create"],
        ["Edit"] = ["Edit", "Permissions", "Roles"],
        ["Delete"] = ["Delete"],
        ["Export"] = ["Export"],
        ["Import"] = ["Import", "Confirm"]
    };

    private static readonly Dictionary<string, string> LegacyModuleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["User"] = "UserManagement"
    };

    private static readonly object InvalidateLock = new();
    private static CancellationTokenSource _allUsersCacheTokenSource = new();

    public async Task EnsureSystemRolesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var adminRole = await roleManager.FindByNameAsync("Admin")
            ?? throw new InvalidOperationException("Không thể khởi tạo role Admin.");
        var permissionIds = await dbContext.ApplicationPermissions.Select(x => x.Id).ToListAsync(cancellationToken);
        var adminGrants = await dbContext.ApplicationRolePermissions
            .Where(x => x.RoleId == adminRole.Id)
            .ToDictionaryAsync(x => x.PermissionId, cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            if (adminGrants.TryGetValue(permissionId, out var grant))
            {
                grant.IsGranted = true;
                continue;
            }

            dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permissionId,
                IsGranted = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureDefaultUserPermissionsAsync(cancellationToken);
    }

    /// <summary>
    /// Đồng bộ bảng ApplicationPermissions với danh sách Controller/Action phát hiện bằng reflection.
    /// </summary>
    public Task SyncAsync(CancellationToken cancellationToken = default) =>
        SyncAsync(Permissions.All(), cancellationToken);

    public async Task SyncAsync(IReadOnlyList<PermissionMetadata> metadata, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ApplicationPermissions
            .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in metadata)
        {
            if (!existing.TryGetValue(item.Key, out var permission))
            {
                permission = new ApplicationPermission { Key = item.Key, Area = string.Empty };
                dbContext.ApplicationPermissions.Add(permission);
            }

            permission.Controller = item.Controller;
            permission.Action = item.Action;
            permission.Module = item.Controller;
            permission.Description = $"{item.ModuleDisplayName} – {item.ActionDisplayName}";
            permission.ModuleDisplayName = item.ModuleDisplayName;
            permission.ActionDisplayName = item.ActionDisplayName;
            permission.Group = item.Group;
            permission.SortOrder = item.SortOrder;
            permission.IsAnonymous = false;
            permission.IsAdminOnly = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var validKeys = metadata.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allPermissions = await dbContext.ApplicationPermissions
            .Include(x => x.RolePermissions)
            .ToListAsync(cancellationToken);
        var currentByKey = allPermissions
            .Where(x => validKeys.Contains(x.Key))
            .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var stalePermissions = allPermissions.Where(x => !validKeys.Contains(x.Key)).ToList();

        foreach (var stale in stalePermissions)
        {
            MigrateLegacyGrants(stale, currentByKey);
            dbContext.ApplicationRolePermissions.RemoveRange(stale.RolePermissions);
            dbContext.ApplicationPermissions.Remove(stale);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateAll();
    }

    private void MigrateLegacyGrants(
        ApplicationPermission stale,
        IReadOnlyDictionary<string, ApplicationPermission> currentByKey)
    {
        var separator = stale.Key.IndexOf('.');
        if (separator <= 0)
            return;

        var module = stale.Key[..separator];
        var action = stale.Key[(separator + 1)..];
        if (!LegacyActionMap.TryGetValue(action, out var targetActions))
            return;

        var controller = LegacyModuleMap.GetValueOrDefault(module, module);
        foreach (var grant in stale.RolePermissions.Where(x => x.IsGranted))
        {
            foreach (var targetAction in targetActions)
            {
                if (!currentByKey.TryGetValue(Permissions.Key(controller, targetAction), out var target))
                    continue;

                var existing = target.RolePermissions.FirstOrDefault(x => x.RoleId == grant.RoleId);
                if (existing is not null)
                {
                    existing.IsGranted = true;
                    continue;
                }

                var migrated = new ApplicationRolePermission
                {
                    RoleId = grant.RoleId,
                    PermissionId = target.Id,
                    IsGranted = true
                };
                target.RolePermissions.Add(migrated);
                dbContext.ApplicationRolePermissions.Add(migrated);
            }
        }
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string permissionKey)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;
        if (user.IsInRole("Admin"))
            return true;

        var keys = await GetGrantedKeysAsync(user);
        return keys.Contains(permissionKey);
    }

    public async Task<HashSet<string>> GetGrantedKeysAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = $"permissions:{userId}";
        if (memoryCache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        var keys = await dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(dbContext.ApplicationRolePermissions,
                userRole => userRole.RoleId,
                rolePermission => rolePermission.RoleId,
                (_, rolePermission) => rolePermission)
            .Where(rolePermission => rolePermission.IsGranted)
            .Join(dbContext.ApplicationPermissions,
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Key)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        CancellationToken allUsersToken;
        lock (InvalidateLock)
            allUsersToken = _allUsersCacheTokenSource.Token;
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .AddExpirationToken(new CancellationChangeToken(allUsersToken));
        memoryCache.Set(cacheKey, keys, options);
        return keys;
    }

    public void InvalidateUser(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            memoryCache.Remove($"permissions:{userId}");
    }

    // Service là scoped nhưng cache là singleton: token phải dùng chung giữa các request
    // thì đổi quyền ở request này mới xoá được cache tạo ở request khác.
    public void InvalidateAll()
    {
        lock (InvalidateLock)
        {
            _allUsersCacheTokenSource.Cancel();
            _allUsersCacheTokenSource.Dispose();
            _allUsersCacheTokenSource = new CancellationTokenSource();
        }
    }

    public async Task EnsureDefaultUserPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var userRole = await roleManager.FindByNameAsync("User");
        if (userRole is null)
            throw new InvalidOperationException("Role User phải được khởi tạo.");

        var hasAnyPermission = await dbContext.ApplicationRolePermissions
            .AnyAsync(x => x.RoleId == userRole.Id, cancellationToken);
        if (hasAnyPermission)
            return;

        var permissions = await dbContext.ApplicationPermissions
            .Where(x => DefaultUserPermissionKeys.Contains(x.Key))
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            dbContext.ApplicationRolePermissions.Add(new ApplicationRolePermission
            {
                RoleId = userRole.Id,
                PermissionId = permission.Id,
                IsGranted = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
