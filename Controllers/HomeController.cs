using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using DemoMVC.Data;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext context;
    public HomeController(ApplicationDbContext context) => this.context = context;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new HomeDashboardViewModel
        {
            StudentCount = await context.Students.CountAsync(),
            DepartmentCount = await context.Departments.CountAsync(),
            EmployeeCount = await context.Employees.CountAsync(),
            ProductCount = await context.Products.CountAsync()
        };
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // Không giới hạn GET: UseStatusCodePagesWithReExecute giữ nguyên phương thức gốc (vd. POST bị 429).
    [AllowAnonymous]
    [Route("Home/StatusCode/{statusCode:int}")]
    public IActionResult StatusCodePage(int statusCode)
    {
        Response.StatusCode = statusCode;
        ViewData["StatusCode"] = statusCode;
        ViewData["StatusDescription"] = ReasonPhrases.GetReasonPhrase(statusCode);
        return View("StatusCode");
    }
}
