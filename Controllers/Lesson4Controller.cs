namespace Lesson4.Controllers
{
    using DemoMVC.Services;
    using Microsoft.AspNetCore.Mvc;

    // Ví dụ định tuyến bằng thuộc tính [Route]: controller có hai tiền tố URL.
    //   /Lesson4            (giữ URL cũ theo route mặc định {controller}/{action}/{id?})
    //   /bai-hoc/dinh-tuyen (URL thân thiện, tiếng Việt không dấu)
    // Khi controller đã dùng [Route], route mặc định không còn áp dụng cho nó nữa,
    // nên mọi action đều phải khai báo template riêng.
    [Route("Lesson4")]
    [Route("bai-hoc/dinh-tuyen")]
    [PermissionModule("Bài 4 – Định tuyến", "Bài học", 81)]
    public class Lesson4Controller : Controller
    {
        // "" khớp với chính tiền tố: /Lesson4 và /bai-hoc/dinh-tuyen; "Index" khớp /Lesson4/Index.
        [HttpGet("")]
        [HttpGet("Index")]
        [PermissionAction("Trang định tuyến, gửi form")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("")]
        [HttpPost("Index")]
        [ValidateAntiForgeryToken]
        public IActionResult Index(string fullName, string address, string university)
        {
            ViewBag.ThongBao = "Xin chào: " + fullName + " - Địa chỉ: " + address + " - Trường: " + university;
            return View();
        }

        // Tham số trên URL và tham số tuỳ chọn: /bai-hoc/dinh-tuyen/chao/Viet hoặc /bai-hoc/dinh-tuyen/chao/Viet/21
        [HttpGet("chao/{name}/{age:int?}")]
        [PermissionAction("Ví dụ tham số URL")]
        public IActionResult Greet(string name, int? age)
        {
            ViewBag.ThongBao = age is null
                ? $"Xin chào {name}! (route: chao/{{name}}/{{age:int?}}, không truyền age)"
                : $"Xin chào {name}, {age} tuổi! (route: chao/{{name}}/{{age:int?}})";
            return View(nameof(Index));
        }

        // Ràng buộc route: id phải là số nguyên ≥ 1. /bai-hoc/dinh-tuyen/san-pham/abc hoặc /san-pham/0 trả về 404,
        // và trang 404 tuỳ biến (Home/StatusCode/404) được hiển thị.
        // Dấu "~/" bỏ qua tiền tố của controller: URL là /san-pham/{id}, không phải /Lesson4/san-pham/{id}.
        [HttpGet("~/san-pham/{id:int:min(1)}")]
        [PermissionAction("Ví dụ ràng buộc route")]
        public IActionResult ProductById(int id)
        {
            ViewBag.ThongBao = $"Đã khớp route ~/san-pham/{{id:int:min(1)}} với id = {id}.";
            return View(nameof(Index));
        }
    }
}
