using DemoMVC.Controllers;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class AccountControllerTests
{
    [Fact]
    public void ConfirmEmail_with_invalid_token_returns_confirmation_result_view()
    {
        var fixture = new ControllerFixture();

        var result = fixture.Controller.ConfirmEmail("user-1", "invalid-token");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ConfirmEmailResult", view.ViewName);
        Assert.False(Assert.IsType<bool>(view.ViewData["Succeeded"]));
    }

    [Fact]
    public async Task ForgotPassword_returns_same_response_for_unknown_and_known_emails()
    {
        var unknownFixture = new ControllerFixture();
        unknownFixture.UserManager
            .Setup(manager => manager.FindByEmailAsync("user@example.test"))
            .ReturnsAsync((ApplicationUser?)null);
        var unknownResult = await unknownFixture.Controller.ForgotPassword(
            new ForgotPasswordViewModel { Email = "user@example.test" },
            CancellationToken.None);

        var knownFixture = new ControllerFixture();
        knownFixture.UserManager
            .Setup(manager => manager.FindByEmailAsync("user@example.test"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = "user-1",
                Email = "user@example.test",
                FullName = "Known User",
                EmailConfirmed = true
            });
        knownFixture.UserManager
            .Setup(manager => manager.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("secret-reset-token");
        knownFixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var knownResult = await knownFixture.Controller.ForgotPassword(
            new ForgotPasswordViewModel { Email = "user@example.test" },
            CancellationToken.None);

        Assert.Equal(unknownFixture.Message(unknownResult), knownFixture.Message(knownResult));
        unknownFixture.EmailSender.Verify(sender => sender.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        knownFixture.EmailSender.Verify(sender => sender.SendAsync(
            "user@example.test",
            "Đặt lại mật khẩu",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_does_not_disclose_email_delivery_failure()
    {
        var fixture = new ControllerFixture();
        fixture.UserManager
            .Setup(manager => manager.FindByEmailAsync("user@example.test"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = "user-1",
                Email = "user@example.test",
                FullName = "Known User",
                EmailConfirmed = true
            });
        fixture.UserManager
            .Setup(manager => manager.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("secret-reset-token");
        fixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("delivery failed", new InvalidOperationException()));

        var result = await fixture.Controller.ForgotPassword(
            new ForgotPasswordViewModel { Email = "user@example.test" },
            CancellationToken.None);

        Assert.Equal(ControllerFixture.GenericForgotPasswordMessage, fixture.Message(result));
    }

    [Fact]
    public async Task Login_disallows_unconfirmed_users_with_clear_message()
    {
        var fixture = new ControllerFixture();
        fixture.SignInManager
            .Setup(manager => manager.PasswordSignInAsync(
                "user@example.test", "BadPassword!123", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);

        var result = await fixture.Controller.Login(new LoginViewModel
        {
            Email = "user@example.test",
            Password = "BadPassword!123"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            fixture.Controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("xác thực email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_reports_lockout_without_exposing_credentials()
    {
        var fixture = new ControllerFixture();
        fixture.SignInManager
            .Setup(manager => manager.PasswordSignInAsync(
                "user@example.test", "BadPassword!123", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        await fixture.Controller.Login(new LoginViewModel
        {
            Email = "user@example.test",
            Password = "BadPassword!123"
        });

        Assert.Contains(
            fixture.Controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("bị khóa", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ControllerFixture
    {
        public const string GenericForgotPasswordMessage =
            "Nếu tài khoản phù hợp tồn tại, hướng dẫn sẽ được gửi tới email đã đăng ký.";

        public Mock<UserManager<ApplicationUser>> UserManager { get; }
        public Mock<SignInManager<ApplicationUser>> SignInManager { get; }
        public Mock<IEmailSender> EmailSender { get; } = new();
        public AccountController Controller { get; }

        public ControllerFixture()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            UserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);

            var principalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            var schemeProvider = new Mock<IAuthenticationSchemeProvider>();
            var userConfirmation = new Mock<IUserConfirmation<ApplicationUser>>();
            SignInManager = new Mock<SignInManager<ApplicationUser>>(
                UserManager.Object,
                new HttpContextAccessor(),
                principalFactory.Object,
                Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<ApplicationUser>>.Instance,
                schemeProvider.Object,
                userConfirmation.Object);

            Controller = new AccountController(
                UserManager.Object,
                SignInManager.Object,
                EmailSender.Object,
                new AuthEmailTemplateRenderer(System.Text.Encodings.Web.HtmlEncoder.Default),
                Options.Create(EmailSettingsValidatorTests.ValidSettings()),
                NullLogger<AccountController>.Instance);

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(helper => helper.Action(It.IsAny<UrlActionContext>()))
                .Returns("/Account/ResetPassword?userId=user-1&token=encoded");
            Controller.Url = urlHelper.Object;
            Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                RouteData = new RouteData()
            };
        }

        public string? Message(IActionResult actionResult)
        {
            var view = Assert.IsType<ViewResult>(actionResult);
            Assert.Equal("EmailRequestConfirmation", view.ViewName);
            return Assert.IsType<string>(view.Model);
        }
    }
}
