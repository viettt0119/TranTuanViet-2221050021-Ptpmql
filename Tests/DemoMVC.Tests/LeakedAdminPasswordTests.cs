using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DemoMVC.Tests;

public class LeakedAdminPasswordTests
{
    private const string LeakedPassword = "AdminPassword!123";

    [Fact]
    public async Task SeedAsync_rotates_leaked_default_password_of_existing_admin()
    {
        await using var harness = await IdentityHarness.CreateAsync();
        var admin = await harness.CreateAdminAsync("admin@demo.local", LeakedPassword);

        await harness.Seeder("admin@demo.local", "Fresh!AdminPass2026").SeedAsync();

        var reloaded = (await harness.UserManager.FindByIdAsync(admin.Id))!;
        Assert.False(await harness.UserManager.CheckPasswordAsync(reloaded, LeakedPassword));
        Assert.True(await harness.UserManager.CheckPasswordAsync(reloaded, "Fresh!AdminPass2026"));
    }

    [Fact]
    public async Task SeedAsync_resets_admin_password_when_reset_flag_is_set()
    {
        await using var harness = await IdentityHarness.CreateAsync();
        var admin = await harness.CreateAdminAsync("admin@example.test", "Forgotten!Pass123");

        await harness.Seeder("admin@example.test", "Recovered!Pass123", resetPassword: true).SeedAsync();

        Assert.True(await harness.UserManager.CheckPasswordAsync(
            (await harness.UserManager.FindByIdAsync(admin.Id))!, "Recovered!Pass123"));
    }

    [Fact]
    public async Task LockAdministrators_locks_only_admins_using_the_leaked_password()
    {
        await using var harness = await IdentityHarness.CreateAsync();
        var leakedAdmin = await harness.CreateAdminAsync("admin@demo.local", LeakedPassword);
        var safeAdmin = await harness.CreateAdminAsync("owner@example.test", "Unique!Owner2026");

        var locked = await harness.Seeder("unused@example.test", "Unused!Pass2026")
            .LockAdministratorsUsingLeakedPasswordsAsync();

        Assert.Equal(1, locked);
        Assert.True(await harness.UserManager.IsLockedOutAsync((await harness.UserManager.FindByIdAsync(leakedAdmin.Id))!));
        Assert.False(await harness.UserManager.IsLockedOutAsync((await harness.UserManager.FindByIdAsync(safeAdmin.Id))!));
    }

    [Fact]
    public async Task SeedAsync_unlocks_admin_after_rotating_a_leaked_password()
    {
        await using var harness = await IdentityHarness.CreateAsync();
        var admin = await harness.CreateAdminAsync("admin@demo.local", LeakedPassword);
        await harness.Seeder("unused@example.test", "Unused!Pass2026").LockAdministratorsUsingLeakedPasswordsAsync();

        await harness.Seeder("admin@demo.local", "Fresh!AdminPass2026").SeedAsync();

        Assert.False(await harness.UserManager.IsLockedOutAsync((await harness.UserManager.FindByIdAsync(admin.Id))!));
    }

    [Fact]
    public void Validator_rejects_the_leaked_default_password()
    {
        var result = new IdentitySeedOptionsValidator().Validate(null, new IdentitySeedOptions
        {
            Email = "admin@demo.local",
            Password = LeakedPassword,
            FullName = "Admin"
        });

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(LeakedPassword, string.Join(";", result.Failures ?? []), StringComparison.Ordinal);
    }

    private sealed class IdentityHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        private IdentityHarness(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
        }

        public UserManager<ApplicationUser> UserManager => _provider.GetRequiredService<UserManager<ApplicationUser>>();
        private RoleManager<IdentityRole> RoleManager => _provider.GetRequiredService<RoleManager<IdentityRole>>();

        public static async Task<IdentityHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options => options.Lockout.AllowedForNewUsers = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            return new IdentityHarness(connection, provider);
        }

        public async Task<ApplicationUser> CreateAdminAsync(string email, string password)
        {
            if (!await RoleManager.RoleExistsAsync("Admin"))
                Assert.True((await RoleManager.CreateAsync(new IdentityRole("Admin"))).Succeeded);

            var user = new ApplicationUser { UserName = email, Email = email, FullName = "Admin", EmailConfirmed = true };
            Assert.True((await UserManager.CreateAsync(user, password)).Succeeded);
            Assert.True((await UserManager.AddToRoleAsync(user, "Admin")).Succeeded);
            return user;
        }

        public IdentitySeeder Seeder(string email, string password, bool resetPassword = false) =>
            new(
                RoleManager,
                UserManager,
                Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
                {
                    Email = email,
                    Password = password,
                    FullName = "Admin",
                    ResetPassword = resetPassword
                }));

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
