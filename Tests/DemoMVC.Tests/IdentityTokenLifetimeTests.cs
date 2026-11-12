using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DemoMVC.Tests;

public class IdentityTokenLifetimeTests
{
    [Fact]
    public async Task Expired_confirmation_token_is_rejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMilliseconds(50));

        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "expiry@example.test",
            Email = "expiry@example.test",
            FullName = "Token Expiry Test"
        };
        Assert.True((await userManager.CreateAsync(user, "Secure!123abc")).Succeeded);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await Task.Delay(TimeSpan.FromMilliseconds(150));
        var result = await userManager.ConfirmEmailAsync(user, token);

        Assert.False(result.Succeeded);
        Assert.False((await userManager.FindByIdAsync(user.Id))!.EmailConfirmed);
    }
}
