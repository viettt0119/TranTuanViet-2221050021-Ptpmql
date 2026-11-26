using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DemoMVC.Models;

public sealed partial class RegisterViewModel : IValidatableObject
{
    [Display(Name = "Tư cách đăng ký")]
    public AccountType AccountType { get; set; } = AccountType.Student;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Mã sinh viên (tư cách Sinh viên) hoặc mã cán bộ (tư cách Giảng viên / Cán bộ).</summary>
    [Display(Name = "Mã sinh viên / Mã cán bộ")]
    [StringLength(20, ErrorMessage = "Mã tối đa 20 ký tự.")]
    public string? MemberCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Mã đã chuẩn hoá (bỏ khoảng trắng, chữ in hoa) để lưu và kiểm tra trùng.</summary>
    public string? NormalizedMemberCode =>
        string.IsNullOrWhiteSpace(MemberCode) ? null : MemberCode.Trim().ToUpperInvariant();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(AccountType))
        {
            yield return new ValidationResult("Tư cách đăng ký không hợp lệ.", [nameof(AccountType)]);
            yield break;
        }

        var code = NormalizedMemberCode;
        if (code is null)
        {
            yield return new ValidationResult($"Vui lòng nhập {AccountType.CodeLabel().ToLowerInvariant()}.", [nameof(MemberCode)]);
            yield break;
        }

        if (AccountType == AccountType.Student && !StudentCodePattern().IsMatch(code))
            yield return new ValidationResult(
                "Mã sinh viên gồm đúng 10 chữ số và bắt đầu bằng 2 (VD: 2521050001).", [nameof(MemberCode)]);

        if (AccountType == AccountType.Staff && !StaffCodePattern().IsMatch(code))
            yield return new ValidationResult(
                "Mã cán bộ gồm 2–4 chữ cái và 2–6 chữ số (VD: GV0123).", [nameof(MemberCode)]);
    }

    public static bool IsValidStudentCode(string? code) => code is not null && StudentCodePattern().IsMatch(code);

    public static bool IsValidStaffCode(string? code) => code is not null && StaffCodePattern().IsMatch(code);

    [GeneratedRegex(@"^2\d{9}$")]
    private static partial Regex StudentCodePattern();

    [GeneratedRegex(@"^[A-Z]{2,4}\d{2,6}$")]
    private static partial Regex StaffCodePattern();
}
