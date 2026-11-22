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

public sealed class StudentFormViewModel
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public StudentGender? Gender { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string University { get; set; } = "Đại học Mỏ - Địa chất";
    public string DepartmentCode { get; set; } = string.Empty;
    public int AdmissionYear { get; set; } = DateTime.Today.Year;
    public string TrainingSystemCode { get; set; } = "21";
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
