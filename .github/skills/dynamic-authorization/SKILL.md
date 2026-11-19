---
name: dynamic-authorization
description: Xây dựng phân quyền động theo Role cho từng action của mọi controller trong ASP.NET Core MVC (Identity RoleClaims + global filter + màn hình ma trận quyền), kèm CRUD Role và gán Role cho người dùng.
---

# Skill: Phân quyền động

## Thiết kế
- Mỗi quyền = chuỗi `"{Controller}.{Action}"` (vd `SinhVien.Create`).
- Lưu quyền được cấp cho Role trong bảng `AspNetRoleClaims` với `ClaimType = "Permission"`, `ClaimValue = "SinhVien.Create"`.
- Role `Admin` luôn được phép (bypass), tránh tự khoá hệ thống.
- Quyền được **tự quét** từ các controller bằng reflection → màn hình ma trận Role × Action luôn đúng với code.

## 1. Quét danh sách quyền
```csharp
public static class PermissionScanner
{
    public static List<PermissionInfo> Scan()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType.IsAssignableTo(typeof(IActionResult)) ||
                            m.ReturnType.IsAssignableTo(typeof(Task<IActionResult>)))
                .Where(m => !m.IsDefined(typeof(NonActionAttribute)) && !m.IsDefined(typeof(AllowAnonymousAttribute)))
                .Select(m => new PermissionInfo(
                    t.Name.Replace("Controller", ""), m.Name,
                    $"{t.Name.Replace("Controller", "")}.{m.Name}")))
            .Distinct().OrderBy(p => p.Controller).ThenBy(p => p.Action).ToList();
    }
}
public record PermissionInfo(string Controller, string Action, string Key);
```
(Action GET/POST cùng tên dùng chung 1 quyền.)

## 2. Global filter kiểm tra quyền
```csharp
public class DynamicPermissionFilter : IAsyncAuthorizationFilter
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IMemoryCache _cache;
    public DynamicPermissionFilter(RoleManager<IdentityRole> roles, IMemoryCache cache) { _roles = roles; _cache = cache; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        if (ctx.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()) return;

        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) { ctx.Result = new ChallengeResult(); return; }
        if (user.IsInRole("Admin")) return;

        var rd = ctx.RouteData.Values;
        var key = $"{rd["controller"]}.{rd["action"]}";

        foreach (var roleName in user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value))
        {
            var perms = await _cache.GetOrCreateAsync($"perm:{roleName}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                var role = await _roles.FindByNameAsync(roleName);
                var claims = role == null ? new List<Claim>() : (await _roles.GetClaimsAsync(role)).ToList();
                return claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToHashSet();
            });
            if (perms!.Contains(key)) return;
        }
        ctx.Result = new ForbidResult();     // → AccessDenied page
    }
}
// Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews(o => o.Filters.Add<DynamicPermissionFilter>());
builder.Services.ConfigureApplicationCookie(o => o.AccessDeniedPath = "/Account/AccessDenied");
```
**Khi sửa quyền của role phải xoá cache** `perm:{roleName}`.

## 3. Các màn hình cần có
- **Role**: CRUD (Modal + Ajax), không cho xoá Role `Admin` hoặc Role đang có người dùng.
- **Gán Role cho User**: danh sách user + checkbox role (`UserManager.AddToRoleAsync/RemoveFromRoleAsync`).
- **Ma trận phân quyền** `Role/Permissions/{roleId}`: nhóm theo Controller, mỗi Action một checkbox, nút "chọn tất cả controller". Lưu bằng `AddClaimAsync/RemoveClaimAsync` trong 1 lần submit.
- Trang `AccessDenied` thân thiện (tiếng Việt).

## Quy tắc
1. Áp dụng cho **tất cả** controller/action; chỉ `[AllowAnonymous]` (đăng nhập, quên mật khẩu, xác thực email) được bỏ qua.
2. Ẩn nút/menu trong View nếu user không có quyền (viết helper `User.HasPermission("SinhVien.Create")`) — nhưng **server vẫn là nơi quyết định**.
3. Không để lại action nhạy cảm nào không qua filter; viết test duyệt tất cả action để xác nhận.
4. Ghi log khi từ chối truy cập.
