using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers;

public class Lec5Controller : Controller
{
    private static readonly List<Lec5Model> Users = new();

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["PageTitle"] = "Create user";
        ViewBag.RoleList = new List<string> { "Admin", "User", "Guest" };
        ViewBag.DemoViewBag = "Dữ liệu này được truyền bằng ViewBag.";
        return View();
    }

    [HttpPost]
    public IActionResult Create(string username, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction(nameof(Create));
        }

        Users.Add(new Lec5Model { Username = username, Role = role });

        TempData["AlertMessage"] = $"User {username} with role {role} created successfully.";
        ViewData["LastCreated"] = $"User {username} with role {role} created successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Title = "Index Page";
        ViewBag.DemoViewBag = "Dữ liệu này được truyền bằng ViewBag .";
        ViewData["DemoViewData"] = "Dữ liệu này được truyền bằng ViewData trên trang Index.";

        return View(Users);
    }
}