using System.ComponentModel.DataAnnotations;
using DemoMVC.Models;

namespace DemoMVC.Models.ViewModels.Students;

public class StudentListItemViewModel
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int AdmissionYear { get; set; }
}

public sealed class StudentDetailsViewModel : StudentListItemViewModel
{
    public DateOnly? DateOfBirth { get; set; }
    public StudentGender? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string University { get; set; } = string.Empty;
    public string TrainingSystemCode { get; set; } = string.Empty;
    public string MajorCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}

// Ràng buộc dữ liệu của form (server + client). Năm/hệ/ngành quyết định 6 chữ số đầu của mã sinh viên,
// nên phải đúng định dạng thì mã mới đủ 10 chữ số.
public sealed class StudentFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Mã sinh viên")]
    public string StudentCode { get; set; } = string.Empty;

    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Giới tính")]
    public StudentGender? Gender { get; set; }

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(254, ErrorMessage = "Email tối đa 254 ký tự.")]
    public string? Email { get; set; }

    [Display(Name = "Số điện thoại")]
    [RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Địa chỉ")]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự.")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Trường đại học")]
    [Required(ErrorMessage = "Vui lòng nhập trường đại học.")]
    [StringLength(150, ErrorMessage = "Tên trường tối đa 150 ký tự.")]
    public string University { get; set; } = "Đại học Mỏ - Địa chất";

    [Display(Name = "Khoa")]
    [Required(ErrorMessage = "Vui lòng chọn Khoa.")]
    [StringLength(20)]
    public string DepartmentCode { get; set; } = string.Empty;

    [Display(Name = "Năm nhập học")]
    [Range(2000, 2099, ErrorMessage = "Năm nhập học từ 2000 đến 2099.")]
    public int AdmissionYear { get; set; } = DateTime.Today.Year;

    [Display(Name = "Mã hệ đào tạo")]
    [Required(ErrorMessage = "Vui lòng nhập mã hệ đào tạo.")]
    [RegularExpression(@"^[0-9]{2}$", ErrorMessage = "Mã hệ gồm đúng 2 chữ số (vd. 21).")]
    public string TrainingSystemCode { get; set; } = "21";

    [Display(Name = "Mã ngành")]
    [Required(ErrorMessage = "Vui lòng nhập mã ngành.")]
    [RegularExpression(@"^[0-9]{2}$", ErrorMessage = "Mã ngành gồm đúng 2 chữ số (vd. 05).")]
    public string MajorCode { get; set; } = "05";

    public Guid ConcurrencyStamp { get; set; }
}

public sealed class StudentIndexViewModel
{
    public StudentFilterViewModel Filter { get; set; } = new();
    public PagedList<StudentListItemViewModel> Students { get; set; } = null!;
    public IReadOnlyList<DepartmentModel> Departments { get; set; } = [];
    public IReadOnlyList<int> AdmissionYears { get; set; } = [];
}
