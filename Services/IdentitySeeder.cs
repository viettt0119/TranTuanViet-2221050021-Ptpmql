using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DemoMVC.Services;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentitySeedOptions> seedOptions)
{
    private static readonly string[] Roles = ["Admin", "User"];

    // Mật khẩu admin mặc định từng bị commit vào appsettings.json (repo công khai) và đã được seed lên production.
    private static readonly string[] KnownLeakedPasswords = ["AdminPassword!123"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"Could not create the '{roleName}' role.");
            }
        }

        var settings = seedOptions.Value;
        var email = settings.Email?.Trim();
        var password = settings.Password?.Trim();
        var fullName = settings.FullName?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Administrator seed settings are incomplete. Configure AdminSeed:Email, AdminSeed:Password, and AdminSeed:FullName.");

        var admin = await userManager.FindByEmailAsync(email);
        var created = admin is null;
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(createResult, "Could not create the configured administrator account.");
        }
        else
        {
            var needsUpdate = false;
            if (!string.Equals(admin.UserName, email, StringComparison.OrdinalIgnoreCase))
            {
                admin.UserName = email;
                needsUpdate = true;
            }
            if (!string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                admin.Email = email;
                needsUpdate = true;
            }
            if (!string.Equals(admin.FullName, fullName, StringComparison.Ordinal))
            {
                admin.FullName = fullName;
                needsUpdate = true;
            }
            if (!admin.EmailConfirmed)
            {
                admin.EmailConfirmed = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(admin);
                EnsureSucceeded(updateResult, "Could not normalize the configured administrator account.");
            }
        }

        var isAdministrator = await userManager.IsInRoleAsync(admin, "Admin");
        // Tài khoản trùng email admin có thể do người khác tự đăng ký trước khi production được cấu hình,
        // hoặc admin cũ còn dùng mật khẩu mặc định đã lộ trong lịch sử git: đặt lại theo AdminSeed.
        if (!created && (!isAdministrator || settings.ResetPassword || await UsesLeakedPasswordAsync(admin)))
            await ApplyConfiguredPasswordAsync(admin, password);

        if (!isAdministrator)
        {
            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
            EnsureSucceeded(roleResult, "Could not assign the administrator role.");
        }
    }

    // Khóa mọi tài khoản Admin còn dùng mật khẩu mặc định đã bị commit công khai. Trả về số tài khoản bị khóa.
    public async Task<int> LockAdministratorsUsingLeakedPasswordsAsync(CancellationToken cancellationToken = default)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
            return 0;

        var locked = 0;
        foreach (var user in await userManager.GetUsersInRoleAsync("Admin"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await UsesLeakedPasswordAsync(user))
                continue;

            EnsureSucceeded(await userManager.SetLockoutEnabledAsync(user, true), "Could not enable lockout.");
            EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), "Could not lock the account.");
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "Could not revoke existing sessions.");
            locked++;
        }

        return locked;
    }

    public static bool IsKnownLeakedPassword(string? password) =>
        password is not null && KnownLeakedPasswords.Contains(password.Trim(), StringComparer.Ordinal);

    private async Task<bool> UsesLeakedPasswordAsync(ApplicationUser user)
    {
        if (!await userManager.HasPasswordAsync(user))
            return false;

        foreach (var leakedPassword in KnownLeakedPasswords)
        {
            if (await userManager.CheckPasswordAsync(user, leakedPassword))
                return true;
        }

        return false;
    }

    private async Task ApplyConfiguredPasswordAsync(ApplicationUser admin, string password)
    {
        if (IsKnownLeakedPassword(password))
            throw new InvalidOperationException("AdminSeed:Password is a publicly leaked default password. Choose a new one.");

        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, admin, password);
            EnsureSucceeded(validation, "AdminSeed:Password does not meet the password policy.");
        }

        if (await userManager.HasPasswordAsync(admin))
        {
            var removeResult = await userManager.RemovePasswordAsync(admin);
            EnsureSucceeded(removeResult, "Could not reset the existing administrator password.");
        }

        var passwordResult = await userManager.AddPasswordAsync(admin, password);
        EnsureSucceeded(passwordResult, "Could not set the configured administrator password.");

        // Mở khóa (kể cả khi trước đó bị khóa vì dùng mật khẩu đã lộ).
        if (await userManager.IsLockedOutAsync(admin))
            EnsureSucceeded(await userManager.SetLockoutEndDateAsync(admin, null), "Could not unlock the administrator account.");
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(admin), "Could not reset failed sign-in attempts.");
    }

    private static void EnsureSucceeded(IdentityResult result, string failureMessage)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{failureMessage} {errors}");
    }
}
