using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models;

[Table("Departments")]
public class DepartmentModel
{
    [Display(Name = "Mã Khoa")]
    [Key, Required(ErrorMessage = "Vui lòng nhập mã Khoa"), StringLength(20)]
    [RegularExpression(@"^[A-Z0-9]+$", ErrorMessage = "Mã Khoa chỉ gồm chữ in hoa A-Z và số")]
    public string DepartmentCode { get; set; } = string.Empty;

    [Display(Name = "Tên Khoa")]
    [Required(ErrorMessage = "Vui lòng nhập tên Khoa"), StringLength(150)]
    public string DepartmentName { get; set; } = string.Empty;

    public ICollection<StudentModel> Students { get; set; } = new List<StudentModel>();
}
