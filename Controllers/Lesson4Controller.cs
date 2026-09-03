namespace Lesson4.Controllers
{
    using DemoMVC.Models;
    using Microsoft.AspNetCore.Mvc;

    public class Lesson4Controller : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(string fullName, string address, string university)
        {
             ViewBag.ThongBao = "Xin chào: " + fullName + " - Địa chỉ: " + address + " - Trường: " + university;
            return View();
        }

    }
}