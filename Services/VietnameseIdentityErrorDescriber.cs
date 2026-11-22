using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Services;

// Thông báo lỗi Identity bằng tiếng Việt (mặc định ASP.NET Core trả về tiếng Anh).
public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Đã xảy ra lỗi không xác định.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Mật khẩu không đúng.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "Liên kết hoặc mã xác thực không hợp lệ hoặc đã hết hạn.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Error(nameof(RecoveryCodeRedemptionFailed), "Mã khôi phục không hợp lệ.");

    public override IdentityError LoginAlreadyAssociated() =>
        Error(nameof(LoginAlreadyAssociated), "Tài khoản đăng nhập ngoài này đã được liên kết với người dùng khác.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), $"Tên đăng nhập '{userName}' không hợp lệ.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), $"Email '{email}' không hợp lệ.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"Tên đăng nhập '{userName}' đã được sử dụng.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), $"Email '{email}' đã được đăng ký.");

    public override IdentityError InvalidRoleName(string? role) =>
        Error(nameof(InvalidRoleName), $"Tên vai trò '{role}' không hợp lệ.");

    public override IdentityError DuplicateRoleName(string role) =>
        Error(nameof(DuplicateRoleName), $"Vai trò '{role}' đã tồn tại.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Người dùng đã có mật khẩu.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "Tài khoản này không bật chức năng khóa.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), $"Người dùng đã có vai trò '{role}'.");

    public override IdentityError UserNotInRole(string role) =>
        Error(nameof(UserNotInRole), $"Người dùng không có vai trò '{role}'.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"Mật khẩu phải có ít nhất {length} ký tự.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"Mật khẩu phải có ít nhất {uniqueChars} ký tự khác nhau.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Mật khẩu phải có ít nhất một ký tự đặc biệt.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Mật khẩu phải có ít nhất một chữ số (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Mật khẩu phải có ít nhất một chữ thường (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Mật khẩu phải có ít nhất một chữ hoa (A-Z).");

    private static IdentityError Error(string code, string description) =>
        new() { Code = code, Description = description };
}
