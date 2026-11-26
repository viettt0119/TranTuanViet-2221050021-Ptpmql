using System.Security.Claims;
using DemoMVC.Controllers;
using DemoMVC.Data;
using DemoMVC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public class HomeControllerTests
{
    [Fact]
    public void Index_shows_the_public_landing_page_to_guests()
    {
        using var context = CreateContext();
        var controller = CreateController(context, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.IsType<ViewResult>(controller.Index());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SystemRoles.User)]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Admin)]
    public void Index_sends_every_signed_in_account_to_the_shared_home(string? role)
    {
        using var context = CreateContext();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "signed-in-user")], "test");
        if (role is not null) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        var controller = CreateController(context, new ClaimsPrincipal(identity));

        var redirect = Assert.IsType<RedirectToActionResult>(controller.Index());

        Assert.Equal(nameof(HomeController.Dashboard), redirect.ActionName);
        Assert.True(redirect.ControllerName is null or "Home");
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options);

    private static HomeController CreateController(ApplicationDbContext context, ClaimsPrincipal user) =>
        new(context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
}
