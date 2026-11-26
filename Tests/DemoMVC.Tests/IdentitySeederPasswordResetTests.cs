using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DemoMVC.Tests;

public class IdentitySeederPasswordResetTests
{
    [Fact]
    public async Task SeedAsync_resets_admin_password_when_reset_flag_is_set()
    {
        await using var harness = await IdentityHarness.CreateAsync();
        var admin = await harness.CreateAdminAsync("admin@example.test", "Forgotten!Pass123");

        await harness.Seeder("admin@example.test", "Recovered!Pass123", resetPassword: true).SeedAsync();

        Assert.True(await harness.UserManager.CheckPasswordAsync(
            (await harness.UserManager.FindByIdAsync(admin.Id))!, "Recovered!Pass123"));
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
