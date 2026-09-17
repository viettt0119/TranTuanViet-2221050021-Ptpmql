using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public class StudentModel
{
    public int Id { get; set; }  

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập trường đại học")]
    public string University { get; set; } = string.Empty;
}