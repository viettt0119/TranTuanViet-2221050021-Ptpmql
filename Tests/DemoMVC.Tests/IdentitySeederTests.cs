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

        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(1, await context.Users.CountAsync());
        var admin = await provider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync("admin@example.test");
        Assert.NotNull(admin);
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
}
