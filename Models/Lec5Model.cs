using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public class Lec5Model
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được quá 100 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn quyền.")]
    public string Role { get; set; } = string.Empty;
}