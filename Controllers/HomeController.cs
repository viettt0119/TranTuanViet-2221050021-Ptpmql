using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using DemoMVC.Data;
using DemoMVC.Services;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext context;
    public HomeController(ApplicationDbContext context) => this.context = context;

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Dashboard))
        : View();

    // Trang chủ chung chỉ cần đăng nhập; từng thẻ số liệu tự ẩn theo quyền Xem danh sách của module.
    [HttpGet]
    [AuthenticatedOnly]
    public async Task<IActionResult> Dashboard()
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
