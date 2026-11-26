namespace DemoMVC.Controllers
{
    using DemoMVC.Services;
    using Microsoft.AspNetCore.Mvc;

    [PermissionModule("Bài giảng 1–6", "Bài học", 80)]
    public class LessonController : Controller
    {
        [PermissionAction("Bài 1")]
        public IActionResult Lesson1() => View();

        [PermissionAction("Bài 2")]
        public IActionResult Lesson2() => View();

        [PermissionAction("Bài 3")]
        public IActionResult Lesson3() => View();

        [PermissionAction("Bài 4")]
        public IActionResult Lesson4() => View();

        [PermissionAction("Bài 5")]
        public IActionResult Lesson5() => View();

        [PermissionAction("Bài 6")]
        public IActionResult Lesson6() => View();
    }
}
