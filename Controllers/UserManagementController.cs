using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[Authorize]
[PermissionModule("Người dùng", "Hệ thống", 60)]
public class UserManagementController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    PermissionCatalogService permissionCatalog) : Controller
{
    private const string AdminRole = SystemRoles.Admin;
    public const string PendingStatus = "pending";

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q, string? role, string? status, AccountType? type, int page = 1, int pageSize = 10)
    {
        var users = userManager.Users.AsQueryable();
        var search = q?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var normalized = search.ToUpperInvariant();
            var lower = search.ToLowerInvariant();
            users = users.Where(user =>
                user.NormalizedEmail!.Contains(normalized) ||
                user.FullName.ToLower().Contains(lower) ||
                (user.MemberCode != null && user.MemberCode.Contains(normalized)));
        }

        if (type is not null && Enum.IsDefined(type.Value))
            users = users.Where(user => user.AccountType == type);
        else
            type = null;
        // "pending": giảng viên / cán bộ đang chờ phê duyệt.
        if (string.Equals(status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            users = users.Where(user => !user.IsApproved);
        else
            status = null;

        var roleFilter = string.IsNullOrWhiteSpace(role) ? null : await roleManager.FindByIdAsync(role);
        if (roleFilter is not null)
        {
            users = users.Where(user => dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id && userRole.RoleId == roleFilter.Id));
        }

        var paged = await PagedList<ApplicationUser>.CreateAsync(users.OrderBy(user => user.Email), page, pageSize);
        var pageUserIds = paged.Items.Select(user => user.Id).ToList();
        var rolesByUser = (await dbContext.UserRoles
                .Where(userRole => pageUserIds.Contains(userRole.UserId))
                .Join(dbContext.Roles, userRole => userRole.RoleId, item => item.Id,
                    (userRole, item) => new { userRole.UserId, item.Name })
                .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.Select(item => item.Name ?? string.Empty).OrderBy(name => name).ToList());

        ViewData["SearchQuery"] = search;
        ViewData["RoleFilter"] = roleFilter?.Id;
        ViewData["StatusFilter"] = status;
        ViewData["TypeFilter"] = type;
        ViewData["PendingCount"] = await userManager.Users.CountAsync(user => !user.IsApproved);
        ViewData["AllRoles"] = await roleManager.Roles.OrderBy(x => x.Name).ToListAsync();
        ViewData["RolesByUser"] = rolesByUser;
        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        ViewData["Roles"] = (await userManager.GetRolesAsync(user)).OrderBy(x => x).ToList();
        ViewData["IsLockedOut"] = await userManager.IsLockedOutAsync(user);
        return PartialView("_DetailsModal", user);
    }

    [HttpGet]
    [PermissionAction("Gán / gỡ vai trò")]
    public async Task<IActionResult> Roles(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        return PartialView("_RolesModal", await BuildRolesModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(string id, string[]? selectedRoles)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var isAdmin = User.IsInRole(AdminRole);
        if (!isAdmin && user.Id == userManager.GetUserId(User))
            return Forbidden("Bạn không thể tự thay đổi vai trò của chính mình.");
        if (!isAdmin && await userManager.IsInRoleAsync(user, AdminRole))
            return Forbidden("Chỉ Admin mới được thay đổi vai trò của quản trị viên.");

        var allRoles = await roleManager.Roles.ToListAsync();
        var selectedIds = (selectedRoles ?? []).ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Except(allRoles.Select(item => item.Id), StringComparer.Ordinal).Any())
        {
            ModelState.AddModelError(string.Empty, "Danh sách vai trò đã thay đổi. Vui lòng mở lại hộp thoại và chọn lại vai trò.");
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_RolesModal", await BuildRolesModelAsync(user));
        }
        var currentNames = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var editable = await GetEditableRoleIdsAsync(allRoles);

        var toAdd = new List<string>();
        var toRemove = new List<string>();
        foreach (var item in allRoles)
        {
            // Vai trò không được phép sửa hiển thị dạng ô bị khoá (không gửi lên): giữ nguyên như hiện tại.
            if (!editable.Contains(item.Id))
                continue;

            var name = item.Name ?? string.Empty;
            var wanted = selectedIds.Contains(item.Id);
            var has = currentNames.Contains(name);
            if (wanted != has)
                (wanted ? toAdd : toRemove).Add(name);
        }

        // Giữ cả kiểm tra Admin cuối cùng trong transaction để hai lần gỡ đồng thời
        // không cùng nhìn thấy một quản trị viên dự phòng rồi gỡ hết Admin.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        if (toRemove.Contains(AdminRole, StringComparer.OrdinalIgnoreCase))
        {
            var admins = await userManager.GetUsersInRoleAsync(AdminRole);
            if (admins.Count == 1 && admins[0].Id == user.Id)
            {
                ModelState.AddModelError(string.Empty, "Không thể gỡ vai trò Admin khỏi quản trị viên cuối cùng.");
                Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                return PartialView("_RolesModal", await BuildRolesModelAsync(user, selectedIds));
            }
        }

        // Identity lưu từng lần gán/gỡ riêng. Một thao tác trong modal phải được ghi trọn vẹn
        // hoặc hoàn tác toàn bộ, kể cả khi bước gỡ vai trò thất bại sau bước gán.
        try
        {
            if (toAdd.Count > 0)
            {
                var result = await userManager.AddToRolesAsync(user, toAdd);
                if (!result.Succeeded)
                    return await RollbackAsync(result);
            }
            if (toRemove.Count > 0)
            {
                var result = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!result.Succeeded)
                    return await RollbackAsync(result);
            }

            await transaction.CommitAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new JsonResult(new { success = false, message = "Tài khoản hoặc vai trò vừa thay đổi. Vui lòng mở lại hộp thoại và thử lại." })
                { StatusCode = StatusCodes.Status409Conflict };
        }

        // Quyền theo action đọc từ CSDL nên có hiệu lực ngay; role trong cookie được làm mới sau tối đa 1 phút
        // (SecurityStampValidator, Program.cs) mà không đăng xuất người dùng.
        permissionCatalog.InvalidateUser(user.Id);
        return Json(new { success = true, message = $"Đã cập nhật vai trò cho {user.Email}." });

        async Task<IActionResult> RollbackAsync(IdentityResult result)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return RolesError(result, await BuildRolesModelAsync(user, selectedIds));
        }
    }

    // Dự phòng khi email xác thực không gửi được: quản trị viên xác thực thủ công.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAction("Xác thực email thủ công")]
    public async Task<IActionResult> ConfirmEmail(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.EmailConfirmed)
            return Json(new { success = true, message = $"{user.Email} đã được xác thực từ trước." });

        user.EmailConfirmed = true;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Forbidden(string.Join(" ", result.Errors.Select(x => x.Description)));

        return Json(new { success = true, message = $"Đã xác thực email {user.Email}." });
    }

    /// <summary>
    /// Phê duyệt tài khoản Giảng viên / Cán bộ tự đăng ký: cho phép đăng nhập và gán role GiangVien.
    /// </summary>
    [HttpGet, ActionName("Approve")]
    [PermissionAction("Phê duyệt giảng viên")]
    public Task<IActionResult> ApproveConfirmation(string id) =>
        // Mở modal bằng cùng quyền Approve với POST; không đòi thêm quyền Details.
        Details(id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAction("Phê duyệt giảng viên")]
    public async Task<IActionResult> Approve(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.AccountType != AccountType.Staff)
            return Forbidden("Chỉ tài khoản Giảng viên / Cán bộ mới cần phê duyệt.");
        if (user.IsApproved && await userManager.IsInRoleAsync(user, SystemRoles.Lecturer))
            return Json(new { success = true, message = $"{user.Email} đã được phê duyệt từ trước." });

        // Identity tự SaveChanges ở mỗi thao tác. Bao cả gán vai trò và phê duyệt trong
        // một transaction để lỗi ở bước sau không để lại tài khoản được duyệt một nửa.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            if (!await roleManager.RoleExistsAsync(SystemRoles.Lecturer))
            {
                var createRole = await roleManager.CreateAsync(new IdentityRole(SystemRoles.Lecturer));
                if (!createRole.Succeeded)
                    return await RollbackAsync(createRole);
            }

            if (!await userManager.IsInRoleAsync(user, SystemRoles.Lecturer))
            {
                var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.Lecturer);
                if (!roleResult.Succeeded)
                    return await RollbackAsync(roleResult);
            }

            if (!user.IsApproved)
            {
                user.IsApproved = true;
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return await RollbackAsync(result);
            }

            await transaction.CommitAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new JsonResult(new { success = false, message = "Tài khoản hoặc vai trò vừa thay đổi. Vui lòng tải lại và thử phê duyệt lần nữa." })
                { StatusCode = StatusCodes.Status409Conflict };
        }

        permissionCatalog.InvalidateUser(user.Id);
        var note = user.EmailConfirmed ? string.Empty : " Người dùng vẫn cần xác thực email trước khi đăng nhập.";
        return Json(new { success = true, message = $"Đã phê duyệt {user.Email} và gán vai trò {SystemRoles.Lecturer}.{note}" });

        async Task<IActionResult> RollbackAsync(IdentityResult result)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new JsonResult(new { success = false, message = string.Join(" ", result.Errors.Select(x => x.Description)) })
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }
    }

    private async Task<UserRolesViewModel> BuildRolesModelAsync(ApplicationUser user, ISet<string>? selectedOverride = null)
    {
        var allRoles = await roleManager.Roles.OrderBy(x => x.Name).ToListAsync();
        var currentNames = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var editable = await GetEditableRoleIdsAsync(allRoles);
        var isSelf = user.Id == userManager.GetUserId(User);
        var isAdmin = User.IsInRole(AdminRole);
        var targetIsAdmin = currentNames.Contains(AdminRole);
        var readOnlyReason = isAdmin ? null
            : isSelf ? "Bạn không thể tự thay đổi vai trò của chính mình."
            : targetIsAdmin ? "Chỉ Admin mới được thay đổi vai trò của quản trị viên."
            : null;

        return new UserRolesViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            ReadOnlyReason = readOnlyReason,
            Roles = allRoles.Select(item => new RoleSelection
            {
                RoleId = item.Id,
                RoleName = item.Name ?? string.Empty,
                IsSelected = selectedOverride?.Contains(item.Id) ?? currentNames.Contains(item.Name ?? string.Empty),
                CanEdit = readOnlyReason is null && editable.Contains(item.Id)
            }).ToList()
        };
    }

    /// <summary>
    /// Admin được gán mọi vai trò. Người khác chỉ được gán/gỡ vai trò (không phải Admin) mà toàn bộ quyền
    /// của nó nằm trong quyền của chính họ, để không thể cấp cho người khác (hay tài khoản phụ) nhiều quyền hơn mình.
    /// </summary>
    private async Task<HashSet<string>> GetEditableRoleIdsAsync(IReadOnlyCollection<IdentityRole> allRoles)
    {
        if (User.IsInRole(AdminRole))
            return allRoles.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        var myKeys = await permissionCatalog.GetGrantedKeysAsync(User);
        var grantsByRole = (await dbContext.ApplicationRolePermissions
                .Where(x => x.IsGranted)
                .Select(x => new { x.RoleId, x.Permission.Key })
                .ToListAsync())
            .GroupBy(x => x.RoleId)
            .ToDictionary(x => x.Key, x => x.Select(item => item.Key).ToList());

        return allRoles
            .Where(item => !string.Equals(item.Name, AdminRole, StringComparison.OrdinalIgnoreCase))
            .Where(item => !grantsByRole.TryGetValue(item.Id, out var keys) || keys.All(myKeys.Contains))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private PartialViewResult RolesError(IdentityResult result, UserRolesViewModel model)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_RolesModal", model);
    }

    private JsonResult Forbidden(string message) =>
        new(new { success = false, message }) { StatusCode = StatusCodes.Status403Forbidden };

    public sealed class UserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ReadOnlyReason { get; set; }
        public List<RoleSelection> Roles { get; set; } = [];
    }

    public sealed class RoleSelection
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool CanEdit { get; set; }
    }
}
