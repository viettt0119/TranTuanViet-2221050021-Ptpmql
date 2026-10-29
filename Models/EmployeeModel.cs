using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class EmployeeModel
    {
        [Key]
        [Required(ErrorMessage = "Vui lòng nhập mã nhân viên")]
        [StringLength(20, ErrorMessage = "Mã nhân viên không được quá 20 ký tự")]
        public string EmpId { get; set; } = default!;

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự")]
        public string FullName { get; set; } = default!;

        [DataType(DataType.DateTime)]
        public DateTime HireDate { get; set; } = DateTime.Now; 
    }
}