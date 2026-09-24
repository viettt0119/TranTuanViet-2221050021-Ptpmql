using System.ComponentModel.DataAnnotations;
namespace DemoMVC.Models
{
    

public class ProductModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "Tên sản phẩm phải từ 5 đến 100 ký tự")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá sản phẩm")]
    [Range(1000, 100000000 , ErrorMessage = "Giá không hợp lệ, vui lòng nhập giá từ 1.000 đến 100.000.000")]
    public decimal Price { get; set; }
}
}