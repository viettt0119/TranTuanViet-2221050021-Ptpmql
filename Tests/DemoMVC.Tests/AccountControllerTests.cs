using DemoMVC.Controllers;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
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
                EmailConfirmed = false
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
    public async Task Login_reports_disallowed_unknown_account_with_generic_message()
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
            error => error.ErrorMessage.Contains("Email hoặc mật khẩu không đúng", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_creates_unconfirmed_account_and_sends_confirmation_email_to_registrant()
    {
        var fixture = new ControllerFixture();
        fixture.SetupSuccessfulRegistration();
        fixture.UserManager
            .Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");
        fixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await fixture.Controller.Register(ControllerFixture.ValidRegistration());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("RegisterConfirmation", view.ViewName);
        Assert.True(Assert.IsType<bool>(view.ViewData["EmailSent"]));
        fixture.UserManager.Verify(manager => manager.CreateAsync(
            It.Is<ApplicationUser>(user =>
                user.Email == "student@gmail.com" &&
                !user.EmailConfirmed &&
                user.FullName == "Test User"),
            "GoodPassword!123"), Times.Once);
        fixture.EmailSender.Verify(sender => sender.SendAsync(
            "student@gmail.com",
            "Xác thực địa chỉ email",
            It.Is<string>(body => body.Contains("https://example.test/Account/", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_keeps_account_and_reports_when_confirmation_email_cannot_be_sent()
    {
        var fixture = new ControllerFixture();
        fixture.SetupSuccessfulRegistration();
        fixture.UserManager
            .Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");
        fixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("SMTP settings are incomplete.", new InvalidOperationException()));

        var result = await fixture.Controller.Register(ControllerFixture.ValidRegistration());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("RegisterConfirmation", view.ViewName);
        Assert.False(Assert.IsType<bool>(view.ViewData["EmailSent"]));
        Assert.Null(view.ViewData["DevelopmentConfirmationLink"]);
        fixture.UserManager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Register_shows_confirmation_link_only_in_development_when_email_fails()
    {
        var fixture = new ControllerFixture(Environments.Development);
        fixture.SetupSuccessfulRegistration();
        fixture.UserManager
            .Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");
        fixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("SMTP settings are incomplete.", new InvalidOperationException()));

        var result = await fixture.Controller.Register(ControllerFixture.ValidRegistration());

        var view = Assert.IsType<ViewResult>(result);
        Assert.StartsWith(
            "https://example.test/Account/",
            Assert.IsType<string>(view.ViewData["DevelopmentConfirmationLink"]),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_with_unconfirmed_email_and_correct_password_offers_resend()
    {
        var fixture = new ControllerFixture();
        var user = new ApplicationUser { Id = "user-1", Email = "user@example.test", FullName = "User" };
        fixture.SignInManager
            .Setup(manager => manager.PasswordSignInAsync("user@example.test", "GoodPassword!123", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);
        fixture.UserManager.Setup(manager => manager.FindByEmailAsync("user@example.test")).ReturnsAsync(user);
        fixture.UserManager.Setup(manager => manager.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        fixture.UserManager.Setup(manager => manager.IsLockedOutAsync(user)).ReturnsAsync(false);
        fixture.UserManager.Setup(manager => manager.CheckPasswordAsync(user, "GoodPassword!123")).ReturnsAsync(true);

        var result = await fixture.Controller.Login(new LoginViewModel
        {
            Email = "user@example.test",
            Password = "GoodPassword!123"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.True(Assert.IsType<bool>(view.ViewData["ShowResendConfirmation"]));
        Assert.Contains(
            fixture.Controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("chưa xác thực email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_with_unconfirmed_email_and_wrong_password_counts_failure_and_stays_generic()
    {
        var fixture = new ControllerFixture();
        var user = new ApplicationUser { Id = "user-1", Email = "user@example.test", FullName = "User" };
        fixture.SignInManager
            .Setup(manager => manager.PasswordSignInAsync("user@example.test", "WrongPassword!123", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);
        fixture.UserManager.Setup(manager => manager.FindByEmailAsync("user@example.test")).ReturnsAsync(user);
        fixture.UserManager.Setup(manager => manager.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        fixture.UserManager.Setup(manager => manager.IsLockedOutAsync(user)).ReturnsAsync(false);
        fixture.UserManager.Setup(manager => manager.CheckPasswordAsync(user, "WrongPassword!123")).ReturnsAsync(false);
        fixture.UserManager.Setup(manager => manager.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await fixture.Controller.Login(new LoginViewModel
        {
            Email = "user@example.test",
            Password = "WrongPassword!123"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewData["ShowResendConfirmation"]);
        Assert.Contains(
            fixture.Controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("Email hoặc mật khẩu không đúng", StringComparison.OrdinalIgnoreCase));
        fixture.UserManager.Verify(manager => manager.AccessFailedAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResendConfirmation_sends_new_link_to_unconfirmed_account()
    {
        var fixture = new ControllerFixture();
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "student@gmail.com",
            FullName = "Student",
            EmailConfirmed = false
        };
        fixture.UserManager.Setup(manager => manager.FindByEmailAsync("student@gmail.com")).ReturnsAsync(user);
        fixture.UserManager
            .Setup(manager => manager.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("confirmation-token");
        fixture.EmailSender
            .Setup(sender => sender.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await fixture.Controller.ResendConfirmation(
            new ForgotPasswordViewModel { Email = "student@gmail.com" },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("EmailRequestConfirmation", view.ViewName);
        fixture.EmailSender.Verify(sender => sender.SendAsync(
            "student@gmail.com",
            "Xác thực địa chỉ email",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task Login_without_return_url_redirects_to_dashboard()
    {
        var fixture = new ControllerFixture();
        fixture.UrlHelper
            .Setup(helper => helper.Action(It.Is<UrlActionContext>(context =>
                context.Controller == "Home" && context.Action == "Dashboard")))
            .Returns("/Home/Dashboard");
        fixture.SignInManager
            .Setup(manager => manager.PasswordSignInAsync("user@example.test", "GoodPassword!123", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await fixture.Controller.Login(new LoginViewModel
        {
            Email = "user@example.test",
            Password = "GoodPassword!123"
        });

        Assert.Equal("/Home/Dashboard", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public async Task ResetPassword_with_undecodable_token_explains_that_link_is_invalid()
    {
        var fixture = new ControllerFixture();
        fixture.SetupPasswordReset(DateTimeOffset.UtcNow.AddMinutes(3));

        var result = await fixture.Controller.ResetPassword(ControllerFixture.ValidPasswordReset("!!"));

        Assert.IsType<ViewResult>(result);
        Assert.False(fixture.Controller.ModelState.IsValid);
        Assert.Contains(
            fixture.Controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage.Contains("không hợp lệ hoặc đã hết hạn", StringComparison.OrdinalIgnoreCase));
        fixture.UserManager.Verify(manager => manager.ResetPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_confirms_email_and_clears_temporary_lockout()
    {
        var fixture = new ControllerFixture();
        var user = fixture.SetupPasswordReset(DateTimeOffset.UtcNow.AddMinutes(3));

        var result = await fixture.Controller.ResetPassword(ControllerFixture.ValidPasswordReset());

        Assert.Equal("ResetPasswordConfirmation", Assert.IsType<ViewResult>(result).ViewName);
        Assert.True(user.EmailConfirmed);
        fixture.UserManager.Verify(manager => manager.SetLockoutEndDateAsync(user, null), Times.Once);
        fixture.UserManager.Verify(manager => manager.ResetAccessFailedCountAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_keeps_long_lockout_set_by_administrator()
    {
        var fixture = new ControllerFixture();
        fixture.SetupPasswordReset(DateTimeOffset.MaxValue);

        var result = await fixture.Controller.ResetPassword(ControllerFixture.ValidPasswordReset());

        Assert.Equal("ResetPasswordConfirmation", Assert.IsType<ViewResult>(result).ViewName);
        fixture.UserManager.Verify(manager => manager.SetLockoutEndDateAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    private sealed class ControllerFixture
    {
        public const string GenericForgotPasswordMessage =
            "Nếu tài khoản phù hợp tồn tại, hướng dẫn sẽ được gửi tới email đã đăng ký.";

        public Mock<UserManager<ApplicationUser>> UserManager { get; }
        public Mock<SignInManager<ApplicationUser>> SignInManager { get; }
        public Mock<IEmailSender> EmailSender { get; } = new();
        public Mock<IUrlHelper> UrlHelper { get; } = new();
        public AccountController Controller { get; }

        public ControllerFixture(string environmentName = "Production")
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
                Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == environmentName),
                NullLogger<AccountController>.Instance);

            UrlHelper.Setup(helper => helper.Action(It.IsAny<UrlActionContext>()))
                .Returns("/Account/ResetPassword?userId=user-1&token=encoded");
            Controller.Url = UrlHelper.Object;
            Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                RouteData = new RouteData()
            };
        }

        public static RegisterViewModel ValidRegistration() => new()
        {
            FullName = "Test User",
            Email = "student@gmail.com",
            Password = "GoodPassword!123",
            ConfirmPassword = "GoodPassword!123"
        };

        public void SetupSuccessfulRegistration()
        {
            UserManager
                .Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), "GoodPassword!123"))
                .ReturnsAsync(IdentityResult.Success);
            UserManager
                .Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);
        }

        public static ResetPasswordViewModel ValidPasswordReset(string? token = null) => new()
        {
            UserId = "user-1",
            Email = "user@example.test",
            Token = token ?? AuthTokenCodec.Encode("reset-token"),
            Password = "NewPassword!123",
            ConfirmPassword = "NewPassword!123"
        };

        public ApplicationUser SetupPasswordReset(DateTimeOffset lockoutEnd)
        {
            var user = new ApplicationUser
            {
                Id = "user-1",
                Email = "user@example.test",
                FullName = "User",
                LockoutEnabled = true,
                LockoutEnd = lockoutEnd
            };
            UserManager.Setup(manager => manager.FindByIdAsync("user-1")).ReturnsAsync(user);
            UserManager
                .Setup(manager => manager.ResetPasswordAsync(user, "reset-token", "NewPassword!123"))
                .ReturnsAsync(IdentityResult.Success);
            UserManager.Setup(manager => manager.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
            UserManager.Setup(manager => manager.IsLockedOutAsync(user)).ReturnsAsync(true);
            UserManager.Setup(manager => manager.SetLockoutEndDateAsync(user, null)).ReturnsAsync(IdentityResult.Success);
            UserManager.Setup(manager => manager.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
            return user;
        }

        public string? Message(IActionResult actionResult)
        {
            var view = Assert.IsType<ViewResult>(actionResult);
            Assert.Equal("EmailRequestConfirmation", view.ViewName);
            return Assert.IsType<string>(view.Model);
        }
    }
}
