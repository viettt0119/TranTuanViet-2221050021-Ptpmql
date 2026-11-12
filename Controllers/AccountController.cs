using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DemoMVC.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender,
    AuthEmailTemplateRenderer emailTemplates,
    IOptions<EmailSettings> emailOptions,
    ILogger<AccountController> logger) : Controller
{
    private const string AuthRateLimitPolicy = "AuthEndpoints";
    private const string ForgotPasswordMessage =
        "Nếu tài khoản phù hợp tồn tại, hướng dẫn sẽ được gửi tới email đã đăng ký.";

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            if (!deleteResult.Succeeded)
                AddIdentityErrors(deleteResult);
            return View(model);
        }

        ViewData["RegisteredEmail"] = user.Email;
        return View("RegisterConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public IActionResult ConfirmEmail(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || !AuthTokenCodec.TryDecode(token, out var decodedToken))
        {
            ViewData["Succeeded"] = false;
            return View("ConfirmEmailResult");
        }

        ViewData["UserId"] = userId;
        ViewData["Token"] = token;
        return View("ConfirmEmailResult");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> ConfirmEmailPost(string userId, string token)
    {
        if (!AuthTokenCodec.TryDecode(token, out var decodedToken))
        {
            ViewData["Succeeded"] = false;
            return View("ConfirmEmailResult");
        }

        var user = await userManager.FindByIdAsync(userId);
        var result = user is null
            ? IdentityResult.Failed()
            : await userManager.ConfirmEmailAsync(user, decodedToken);

        ViewData["Succeeded"] = result.Succeeded;
        return View("ConfirmEmailResult");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Content("~/"));

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau."
                : "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmation() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> ResendConfirmation(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            try
            {
                await SendConfirmationEmailAsync(user, cancellationToken);
            }
            catch (EmailDeliveryException ex)
            {
                logger.LogError("Confirmation email resend failed ({ExceptionType}).", ex.GetType().Name);
            }
        }

        return View("EmailRequestConfirmation", ForgotPasswordMessage);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = AuthTokenCodec.Encode(token);
            var resetLink = BuildAccountLink(nameof(ResetPassword), new
            {
                userId = user.Id,
                email = user.Email,
                token = encodedToken
            });

            try
            {
                await emailSender.SendAsync(
                    user.Email!,
                    "Đặt lại mật khẩu",
                    emailTemplates.PasswordReset(user.FullName, resetLink),
                    cancellationToken);
            }
            catch (EmailDeliveryException ex)
            {
                logger.LogError("Password reset email delivery failed ({ExceptionType}).", ex.GetType().Name);
            }
        }

        return View("EmailRequestConfirmation", ForgotPasswordMessage);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? userId, string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email) ||
            !AuthTokenCodec.TryDecode(token, out var decodedToken))
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel
        {
            UserId = userId,
            Email = email,
            Token = AuthTokenCodec.Encode(decodedToken)
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid || !AuthTokenCodec.TryDecode(model.Token, out var decodedToken))
            return View(model);

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user is null || !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        return View("ResetPasswordConfirmation");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        try
        {
            await emailSender.SendAsync(
                user.Email!,
                "Mật khẩu đã được thay đổi",
                emailTemplates.PasswordChanged(user.FullName),
                cancellationToken);
            ViewData["StatusMessage"] = "Đã đổi mật khẩu và gửi email thông báo.";
        }
        catch (EmailDeliveryException ex)
        {
            logger.LogError("Password changed notification delivery failed ({ExceptionType}).", ex.GetType().Name);
            ViewData["StatusMessage"] = "Mật khẩu đã đổi, nhưng email thông báo chưa gửi được.";
        }

        return View("ChangePasswordConfirmation");
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = BuildAccountLink(nameof(ConfirmEmail), new
        {
            userId = user.Id,
            token = AuthTokenCodec.Encode(token)
        });
        await emailSender.SendAsync(
            user.Email!,
            "Xác thực địa chỉ email",
            emailTemplates.Confirmation(user.FullName, confirmationLink),
            cancellationToken);
    }

    private string BuildAccountLink(string action, object values)
    {
        var path = Url.Action(action, "Account", values);
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidOperationException("Could not generate an account link.");

        var baseUri = new Uri(emailOptions.Value.PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(baseUri, path.TrimStart('/')).AbsoluteUri;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }
}
