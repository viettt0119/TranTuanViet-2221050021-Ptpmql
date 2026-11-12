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

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
            EnsureSucceeded(roleResult, "Could not assign the administrator role.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string failureMessage)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{failureMessage} {errors}");
    }
}
