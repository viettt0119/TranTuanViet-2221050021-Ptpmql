namespace DemoMVC.Controllers
{
    using DemoMVC.Models;
    using DemoMVC.Services;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Các bài giảng Lec 1–6 và trang "Xem lại bài học". Chỉ tài khoản Admin truy cập được;
    /// không nằm trên ma trận phân quyền động nên không thể cấp cho role khác.
    /// </summary>
    [AdminOnly]
    public class LessonController : Controller
    {
        public static readonly IReadOnlyList<LessonCatalogItem> Catalog =
        [
            new(1, "Bài giảng", "Tổng quan kiến trúc .NET MVC",
                "Mô hình Model – View – Controller, vòng đời request và cấu trúc thư mục của dự án ASP.NET Core MVC.",
                "Lesson", nameof(Lesson1), "bi-diagram-3", ["MVC", "Kiến trúc", "Request pipeline"]),
            new(2, "Bài giảng", "Định tuyến (Routing)",
                "Route mặc định {controller}/{action}/{id?}, tham số tuỳ chọn và cách URL tìm đến action.",
                "Lesson", nameof(Lesson2), "bi-signpost-split", ["Routing", "URL", "Route template"]),
            new(3, "Bài giảng", "Gửi & nhận dữ liệu",
                "Truyền dữ liệu Controller ↔ View: Model, ViewBag, ViewData, form POST và model binding.",
                "Lesson", nameof(Lesson3), "bi-arrow-left-right", ["ViewBag", "ViewData", "Model binding", "Form"]),
            new(4, "Bài giảng", "Quản lý trạng thái dữ liệu tạm thời",
                "TempData, Session, Cookie và khi nào dùng từng loại.",
                "Lesson", nameof(Lesson4), "bi-hourglass-split", ["TempData", "Session", "Cookie"]),
            new(4, "Thực hành", "Định tuyến bằng thuộc tính [Route]",
                "Thực hành buổi 4: tiền tố route, tham số URL, ràng buộc route và gửi form.",
                "Lesson4", "Index", "bi-code-square", ["Attribute routing", "[Route]", "Route constraint"]),
            new(5, "Bài giảng", "Tích hợp Bootstrap 5",
                "Layout, grid, component và cách đưa Bootstrap 5 vào ASP.NET Core MVC.",
                "Lesson", nameof(Lesson5), "bi-bootstrap", ["Bootstrap 5", "Layout", "Responsive"]),
            new(5, "Thực hành", "CRUD người dùng demo bằng Modal + AJAX",
                "Thực hành buổi 5: CRUD trên dữ liệu trong bộ nhớ, ViewBag/ViewData, tìm kiếm và phân trang.",
                "Lec5", "Index", "bi-window-stack", ["CRUD", "Modal", "AJAX", "Phân trang"]),
            new(6, "Bài giảng", "Cấu hình Entity Framework Core",
                "Cài package, chuỗi kết nối, entity, DbContext, migration và cập nhật CSDL SQLite/SQL Server.",
                "Lesson", nameof(Lesson6), "bi-database-gear", ["EF Core", "Migration", "DbContext", "SQLite"])
        ];

        [HttpGet]
        public IActionResult Index(string? q, int? lecture, int page = 1, int pageSize = 10)
        {
            var search = q?.Trim();
            IEnumerable<LessonCatalogItem> items = Catalog;
            if (!string.IsNullOrEmpty(search))
                items = items.Where(item => item.Matches(search));
            if (lecture is >= 1 and <= 6)
                items = items.Where(item => item.Lecture == lecture);

            ViewData["SearchQuery"] = search;
            ViewData["LectureFilter"] = lecture;
            return View(PagedList<LessonCatalogItem>.Create(items, page, pageSize));
        }

        public IActionResult Lesson1() => View();

        public IActionResult Lesson2() => View();

        public IActionResult Lesson3() => View();

        public IActionResult Lesson4() => View();

        public IActionResult Lesson5() => View();

        public IActionResult Lesson6() => View();
    }
}
