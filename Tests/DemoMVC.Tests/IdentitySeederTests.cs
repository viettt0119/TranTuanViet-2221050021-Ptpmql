using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DemoMVC.Tests;

public class IdentitySeederTests
{
    [Fact]
    public async Task SeedAsync_is_idempotent_and_assigns_admin_role()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        var seeder = new IdentitySeeder(
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
            {
                Email = "admin@example.test",
                Password = "SecureAdmin!234",
                FullName = "Test Administrator"
            }));

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(SystemRoles.All.Count, await context.Roles.CountAsync()); // Admin, User, GiangVien
        Assert.Equal(1, await context.Users.CountAsync());
        var admin = await provider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync("admin@example.test");
        Assert.NotNull(admin);
        Assert.Equal(AccountType.Staff, admin!.AccountType); // Admin đăng nhập bằng tư cách "Giảng viên / Quản trị".
        Assert.True(admin.IsApproved);
        Assert.True(admin.EmailConfirmed);
        Assert.True(await provider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(admin, "Admin"));
        Assert.False(await provider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(admin, "User"));
    }

    [Fact]
    public async Task SeedAsync_confirms_existing_configured_admin_without_creating_duplicate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingAdmin = new ApplicationUser
        {
            UserName = "admin@example.test",
            Email = "admin@example.test",
            FullName = "Existing Admin",
            EmailConfirmed = false
        };
        Assert.True((await userManager.CreateAsync(existingAdmin, "SecureAdmin!234")).Succeeded);

        var seeder = new IdentitySeeder(
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            userManager,
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
            {
                Email = "admin@example.test",
                Password = "NotUsed!123456",
                FullName = "Configured Admin"
            }));
        await seeder.SeedAsync();

        Assert.Equal(1, await context.Users.CountAsync());
        Assert.True((await userManager.FindByIdAsync(existingAdmin.Id))!.EmailConfirmed);
        Assert.True(await userManager.IsInRoleAsync(existingAdmin, "Admin"));
    }

    [Fact]
    public async Task SeedAsync_resets_password_of_account_registered_with_admin_email_before_promotion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var squatter = new ApplicationUser
        {
            UserName = "admin@example.test",
            Email = "admin@example.test",
            FullName = "Somebody Else",
            EmailConfirmed = false
        };
        Assert.True((await userManager.CreateAsync(squatter, "Squatter!Pass123")).Succeeded);

        var seeder = new IdentitySeeder(
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            userManager,
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
            {
                Email = "admin@example.test",
                Password = "Configured!Admin123",
                FullName = "Configured Admin"
            }));
        await seeder.SeedAsync();

        var admin = (await userManager.FindByIdAsync(squatter.Id))!;
        Assert.True(await userManager.IsInRoleAsync(admin, "Admin"));
        Assert.False(await userManager.CheckPasswordAsync(admin, "Squatter!Pass123"));
        Assert.True(await userManager.CheckPasswordAsync(admin, "Configured!Admin123"));
    }

    [Fact]
    public async Task SeedAsync_keeps_password_of_existing_administrator()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roleManager.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        var admin = new ApplicationUser
        {
            UserName = "admin@example.test",
            Email = "admin@example.test",
            FullName = "Admin",
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(admin, "Changed!ByAdmin123")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(admin, "Admin")).Succeeded);

        var seeder = new IdentitySeeder(
            roleManager,
            userManager,
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
            {
                Email = "admin@example.test",
                Password = "Original!Seed123",
                FullName = "Admin"
            }));
        await seeder.SeedAsync();

        Assert.True(await userManager.CheckPasswordAsync((await userManager.FindByIdAsync(admin.Id))!, "Changed!ByAdmin123"));
    }
}
