using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

/// <summary>
/// Mã sinh viên / mã cán bộ không được trùng giữa các tài khoản. Chạy trong UserManager.CreateAsync/UpdateAsync
/// nên áp dụng cho mọi nơi tạo tài khoản; index duy nhất IX_AspNetUsers_MemberCode là lớp chặn cuối ở CSDL.
/// </summary>
public sealed class MemberCodeUserValidator : IUserValidator<ApplicationUser>
{
    public const string DuplicateCode = "DuplicateMemberCode";

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.MemberCode))
            return IdentityResult.Success;

        var code = user.MemberCode;
        var taken = await manager.Users.AnyAsync(other => other.MemberCode == code && other.Id != user.Id);
        return taken
            ? IdentityResult.Failed(new IdentityError
            {
                Code = DuplicateCode,
                Description = $"{user.AccountType.CodeLabel()} {code} đã được dùng cho tài khoản khác."
            })
            : IdentityResult.Success;
    }
}
