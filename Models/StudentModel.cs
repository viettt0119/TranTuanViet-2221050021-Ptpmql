using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Models;

[Index(nameof(StudentCode), IsUnique = true)]
[Index(nameof(DepartmentCode))]
[Index(nameof(AdmissionYear))]
[Index(nameof(Email), IsUnique = true)]
public class StudentModel
{
    public int Id { get; set; }

    [Display(Name = "Mã sinh viên")]
    [Required, StringLength(10, MinimumLength = 10)]
    public string StudentCode { get; set; } = string.Empty;

    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Địa chỉ")]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Trường đại học")]
    [Required(ErrorMessage = "Vui lòng nhập trường đại học")]
    public string University { get; set; } = "Đại học Mỏ - Địa chất";

    [Display(Name = "Khoa")]
    [Required(ErrorMessage = "Vui lòng chọn Khoa"), StringLength(20)]
    public string DepartmentCode { get; set; } = string.Empty;

    // Include() dùng navigation này để lấy tên Khoa của sinh viên.
    [ForeignKey(nameof(DepartmentCode))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public DepartmentModel? Department { get; set; }

    [Display(Name = "Năm nhập học")]
    [Range(2000, 2099, ErrorMessage = "Năm nhập học từ 2000 đến 2099")]
    public int AdmissionYear { get; set; } = DateTime.Today.Year;

    [Display(Name = "Mã hệ đào tạo")]
    [Required, RegularExpression(@"^[0-9]{2}$", ErrorMessage = "Mã hệ gồm đúng 2 chữ số")]
    public string TrainingSystemCode { get; set; } = "21";

    [Display(Name = "Mã ngành")]
    [Required, RegularExpression(@"^[0-9]{2}$", ErrorMessage = "Mã ngành gồm đúng 2 chữ số")]
    public string MajorCode { get; set; } = "05";

    [Display(Name = "Ngày sinh")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Giới tính")]
    public StudentGender? Gender { get; set; }

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng"), StringLength(254)]
    public string? Email { get; set; }

    [Display(Name = "Số điện thoại")]
    [RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0")]
    [StringLength(10)]
    public string? PhoneNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

public enum StudentGender
{
    Nam,
    Nữ,
    Khác
}
