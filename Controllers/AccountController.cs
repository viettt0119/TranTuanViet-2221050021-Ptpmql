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
    IHostEnvironment environment,
    ILogger<AccountController> logger) : Controller
{
    private const string AuthRateLimitPolicy = "AuthEndpoints";
    private const string ForgotPasswordMessage =
        "Nếu tài khoản phù hợp tồn tại, hướng dẫn sẽ được gửi tới email đã đăng ký.";
    private const string ResendConfirmationMessage =
        "Nếu tài khoản tồn tại và chưa xác thực, email xác thực mới sẽ được gửi tới địa chỉ đã đăng ký. Hãy kiểm tra cả thư mục Spam/Quảng cáo.";

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> Register(
        RegisterViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return View(model);

        var isStaff = model.AccountType == AccountType.Staff;
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            AccountType = model.AccountType,
            MemberCode = model.NormalizedMemberCode,
            // Giảng viên / Cán bộ tự đăng ký phải chờ Quản trị viên phê duyệt (UserManagement/Approve).
            IsApproved = !isStaff,
            // Tài khoản chỉ đăng nhập được sau khi bấm liên kết xác thực gửi tới email người đăng ký.
            EmailConfirmed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        // Sinh viên nhận role User ngay; giảng viên nhận role GiangVien khi được phê duyệt.
        if (!isStaff)
        {
            var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.User);
            if (!roleResult.Succeeded)
            {
                var deleteResult = await userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                if (!deleteResult.Succeeded)
                    AddIdentityErrors(deleteResult);
                return View(model);
            }
        }

        var delivery = await TrySendConfirmationEmailAsync(user, cancellationToken);
        ViewData["RegisteredEmail"] = user.Email;
        ViewData["PendingApproval"] = isStaff;
        ViewData["EmailSent"] = delivery.Sent;
        if (!delivery.Sent && environment.IsDevelopment())
            ViewData["DevelopmentConfirmationLink"] = delivery.Link;

        return View("RegisterConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public IActionResult ConfirmEmail(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) || !AuthTokenCodec.TryDecode(token, out _))
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
    public IActionResult Login(string? returnUrl = null, AccountType accountType = AccountType.Student) =>
        View(new LoginViewModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            AccountType = Enum.IsDefined(accountType) ? accountType : AccountType.Student
        });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Sai tư cách hoặc giảng viên chưa được duyệt: kiểm tra mật khẩu nhưng không cấp cookie đăng nhập.
        var account = await userManager.FindByEmailAsync(model.Email.Trim());
        if (account is not null && (account.AccountType != model.AccountType || !account.IsApproved))
            return await RejectAccountGateAsync(account, model);

        var result = await signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(IsDeepLink(model.ReturnUrl)
                ? model.ReturnUrl!
                : await GetHomeUrlAsync(account));

        if (result.IsNotAllowed)
        {
            var unconfirmedResult = await CheckUnconfirmedAccountAsync(model.Email.Trim(), model.Password);
            if (unconfirmedResult == UnconfirmedLoginResult.PasswordValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản chưa xác thực email. Hãy mở hộp thư (kể cả thư mục Spam/Quảng cáo) và bấm liên kết xác thực, hoặc gửi lại email xác thực.");
                ViewData["ShowResendConfirmation"] = true;
                return View(model);
            }

            if (unconfirmedResult == UnconfirmedLoginResult.LockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau.");
                return View(model);
            }
        }

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau."
                : "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    [Authorize]
    [AuthenticatedOnly]
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

    // Trang "Tài khoản của tôi": chỉ cần đăng nhập và luôn lấy dữ liệu của chính người đang đăng nhập (không nhận id).
    [Authorize]
    [AuthenticatedOnly]
    [HttpGet]
    public async Task<IActionResult> Profile(
        [FromServices] StudentService studentService,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        return View(new AccountProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            AccountType = user.AccountType,
            MemberCode = user.MemberCode,
            Student = await studentService.GetOwnProfileAsync(user, cancellationToken)
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmation(string? email = null) =>
        View(new ForgotPasswordViewModel { Email = email?.Trim() ?? string.Empty });

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
            await TrySendConfirmationEmailAsync(user, cancellationToken);

        return View("EmailRequestConfirmation", ResendConfirmationMessage);
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
                LogDeliveryFailure("Password reset", ex);
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
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByIdAsync(model.UserId);
        if (!AuthTokenCodec.TryDecode(model.Token, out var decodedToken) ||
            user is null || !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
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

        // Người dùng nhận link qua email nên địa chỉ email đã được chứng minh là của họ.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            LogIfFailed(await userManager.UpdateAsync(user), "confirm the email");
        }

        await ClearTemporaryLockoutAsync(user);

        return View("ResetPasswordConfirmation");
    }

    [Authorize]
    [AuthenticatedOnly]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [AuthenticatedOnly]
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

        await signInManager.RefreshSignInAsync(user);

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
            LogDeliveryFailure("Password changed notification", ex);
            ViewData["StatusMessage"] = "Mật khẩu đã đổi, nhưng email thông báo chưa gửi được.";
        }

        return View("ChangePasswordConfirmation");
    }

    private async Task<(bool Sent, string? Link)> TrySendConfirmationEmailAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        string? confirmationLink = null;
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            confirmationLink = BuildAccountLink(nameof(ConfirmEmail), new
            {
                userId = user.Id,
                token = AuthTokenCodec.Encode(token)
            });
            await emailSender.SendAsync(
                user.Email!,
                "Xác thực địa chỉ email",
                emailTemplates.Confirmation(user.FullName, confirmationLink),
                cancellationToken);
            return (true, confirmationLink);
        }
        catch (EmailDeliveryException ex)
        {
            LogDeliveryFailure("Confirmation", ex);
            return (false, confirmationLink);
        }
    }

    // Trang giới thiệu không phải đích cần quay lại: đăng nhập từ đó vẫn đi tiếp tới trang theo vai trò.
    private bool IsDeepLink(string? returnUrl)
    {
        if (!Url.IsLocalUrl(returnUrl))
            return false;

        var path = returnUrl!.Split('?', '#')[0].TrimStart('~').TrimEnd('/');
        return path.Length > 0 &&
            !path.Equals("/Home", StringComparison.OrdinalIgnoreCase) &&
            !path.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase);
    }

    // Admin vào "Tổng quan quản trị", tài khoản còn lại vào "Tài khoản của tôi" (không cần quyền nghiệp vụ nào).
    // Đọc role từ CSDL vì User của request này vẫn là khách, cookie mới có hiệu lực từ request sau.
    private async Task<string> GetHomeUrlAsync(ApplicationUser? account)
    {
        var isAdmin = account is not null && await userManager.IsInRoleAsync(account, SystemRoles.Admin);
        return isAdmin
            ? Url.Action("Dashboard", "Home")!
            : Url.Action(nameof(Profile), "Account")!;
    }

    /// <summary>
    /// Tài khoản tồn tại nhưng chọn sai tư cách hoặc chưa được duyệt. Chỉ nói rõ lý do khi mật khẩu đúng,
    /// để người không biết mật khẩu không dò được tư cách / trạng thái của email; sai mật khẩu vẫn tính vào khoá tài khoản.
    /// </summary>
    private async Task<IActionResult> RejectAccountGateAsync(ApplicationUser account, LoginViewModel model)
    {
        var check = await signInManager.CheckPasswordSignInAsync(account, model.Password, lockoutOnFailure: true);
        if (check.Succeeded)
        {
            ModelState.AddModelError(string.Empty, account.AccountType != model.AccountType
                ? $"Tài khoản này đăng ký với tư cách {account.AccountType.DisplayName()}. Vui lòng chọn đúng tư cách đăng nhập."
                : "Tài khoản Giảng viên / Cán bộ đang chờ Quản trị viên phê duyệt. Bạn sẽ đăng nhập được sau khi được duyệt.");
            return View(model);
        }

        if (check.IsNotAllowed)
        {
            // CheckPasswordSignInAsync từ chối email chưa xác thực trước khi kiểm tra mật khẩu.
            var unconfirmed = await CheckUnconfirmedAccountAsync(account.Email ?? model.Email.Trim(), model.Password);
            if (unconfirmed == UnconfirmedLoginResult.PasswordValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản chưa xác thực email. Hãy mở hộp thư (kể cả thư mục Spam/Quảng cáo) và bấm liên kết xác thực, hoặc gửi lại email xác thực.");
                ViewData["ShowResendConfirmation"] = true;
                return View(model);
            }
            if (unconfirmed == UnconfirmedLoginResult.LockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau.");
                return View(model);
            }
        }

        ModelState.AddModelError(
            string.Empty,
            check.IsLockedOut
                ? "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau."
                : "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    private async Task<UnconfirmedLoginResult> CheckUnconfirmedAccountAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || await userManager.IsEmailConfirmedAsync(user))
            return UnconfirmedLoginResult.NotApplicable;
        if (await userManager.IsLockedOutAsync(user))
            return UnconfirmedLoginResult.LockedOut;
        if (await userManager.CheckPasswordAsync(user, password))
            return UnconfirmedLoginResult.PasswordValid;

        // SignInManager không kiểm tra mật khẩu khi email chưa xác thực; tự ghi nhận lần sai để giữ cơ chế khóa tài khoản.
        await userManager.AccessFailedAsync(user);
        return UnconfirmedLoginResult.NotApplicable;
    }

    // Đặt lại mật khẩu xong thì gỡ khóa tạm do nhập sai. Khóa dài hơn thời gian khóa mặc định
    // là do quản trị đặt nên giữ nguyên, tránh người bị chặn tự mở khóa qua email.
    private async Task ClearTemporaryLockoutAsync(ApplicationUser user)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            if (user.LockoutEnd > DateTimeOffset.UtcNow.Add(userManager.Options.Lockout.DefaultLockoutTimeSpan))
                return;

            LogIfFailed(await userManager.SetLockoutEndDateAsync(user, null), "unlock the account");
        }

        LogIfFailed(await userManager.ResetAccessFailedCountAsync(user), "reset failed sign-in attempts");
    }

    // Mật khẩu đã đổi xong nên không báo lỗi cho người dùng; chỉ ghi log để quản trị xử lý.
    private void LogIfFailed(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            logger.LogError(
                "Password was reset but could not {Operation}: {Errors}",
                operation,
                string.Join("; ", result.Errors.Select(error => error.Code)));
        }
    }

    private string BuildAccountLink(string action, object values)
    {
        var baseUri = TryGetConfiguredPublicBaseUri();
        if (baseUri is null)
        {
            // Chưa cấu hình Email:PublicBaseUrl: dùng scheme + domain của request hiện tại.
            var absoluteUrl = Url.Action(action, "Account", values, Request.Scheme, Request.Host.Value);
            if (string.IsNullOrWhiteSpace(absoluteUrl))
                throw new InvalidOperationException("Could not generate an account link.");
            return absoluteUrl;
        }

        var path = Url.Action(action, "Account", values);
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidOperationException("Could not generate an account link.");

        return new Uri(baseUri, path.TrimStart('/')).AbsoluteUri;
    }

    private Uri? TryGetConfiguredPublicBaseUri()
    {
        string? publicBaseUrl;
        try
        {
            publicBaseUrl = emailOptions.Value.PublicBaseUrl;
        }
        catch (OptionsValidationException)
        {
            // Cấu hình SMTP chưa đủ thì việc gửi sẽ báo lỗi riêng; ở đây chỉ cần tạo được liên kết.
            return null;
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl) || !EmailSettingsValidator.IsSafePublicBaseUrl(publicBaseUrl))
            return null;

        return new Uri(publicBaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute);
    }

    private void LogDeliveryFailure(string emailKind, EmailDeliveryException exception) =>
        logger.LogError(
            "{EmailKind} email delivery failed: {Reason} ({InnerExceptionType}).",
            emailKind,
            exception.Message,
            exception.InnerException?.GetType().Name ?? exception.GetType().Name);

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var field = error.Code == MemberCodeUserValidator.DuplicateCode ? nameof(RegisterViewModel.MemberCode) : string.Empty;
            ModelState.AddModelError(field, error.Description);
        }
    }

    private enum UnconfirmedLoginResult
    {
        NotApplicable,
        PasswordValid,
        LockedOut
    }
}
