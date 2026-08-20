namespace DemoMVC.Controllers
{

    using Microsoft.AspNetCore.Mvc;

    public class DemoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Welcome()
        {
            ViewData["Message"] = "Your welcome message";

            return View();
        }
    }
}