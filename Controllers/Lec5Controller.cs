using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace DemoMVC.Controllers;

public class Lec5Controller : Controller
{
    private static readonly ConcurrentDictionary<int, Lec5Model> Users = new();
    private static int _nextUserId;
    private static readonly string[] Roles = ["Admin", "User", "Guest"];

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["PageTitle"] = "Create user";
        ViewData["FormActionUrl"] = Url.Action(nameof(Create));
        ViewBag.RoleList = Roles;
        return PartialView("_FormModal", new Lec5Model());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind("Username,Role")] Lec5Model user)
    {
        ValidateRole(user.Role);
        if (!ModelState.IsValid)
        {
            return FormModal(user, nameof(Create));
        }

        user.Id = Interlocked.Increment(ref _nextUserId);
        Users[user.Id] = user;
        return Json(new { success = true, message = $"Đã tạo người dùng {user.Username}." });
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Title = "Index Page";
        ViewBag.DemoViewBag = "Dữ liệu này được truyền bằng ViewBag .";
        ViewData["DemoViewData"] = "Dữ liệu này được truyền bằng ViewData trên trang Index.";

        return View(Users.Values.OrderBy(user => user.Id).ToList());
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        return Users.TryGetValue(id, out var user)
            ? PartialView("_DetailsModal", user)
            : NotFound();
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!Users.TryGetValue(id, out var user))
            return NotFound();

        ViewData["PageTitle"] = "Sửa người dùng";
        ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id });
        ViewBag.RoleList = Roles;
        return PartialView("_FormModal", user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, [Bind("Username,Role")] Lec5Model form)
    {
        ValidateRole(form.Role);
        if (!ModelState.IsValid)
            return FormModal(form, nameof(Edit), new { id });

        if (!Users.TryGetValue(id, out var existingUser))
            return NotFound();

        var updatedUser = new Lec5Model
        {
            Id = id,
            Username = form.Username,
            Role = form.Role
        };
        if (!Users.TryUpdate(id, updatedUser, existingUser))
            return Conflict("Người dùng vừa được thay đổi. Tải lại trang và thử lại.");

        return Json(new { success = true, message = $"Đã cập nhật người dùng {updatedUser.Username}." });
    }

    [HttpGet]
    public IActionResult Delete(int id)
    {
        return Users.TryGetValue(id, out var user)
            ? PartialView("_DeleteModal", user)
            : NotFound();
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        if (!Users.TryRemove(id, out var user))
            return NotFound();

        return Json(new { success = true, message = $"Đã xóa người dùng {user.Username}." });
    }

    private PartialViewResult FormModal(Lec5Model user, string action, object? routeValues = null)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        ViewData["PageTitle"] = action == nameof(Create) ? "Thêm người dùng" : "Sửa người dùng";
        ViewData["FormActionUrl"] = Url.Action(action, routeValues);
        ViewBag.RoleList = Roles;
        return PartialView("_FormModal", user);
    }

    private void ValidateRole(string? role)
    {
        if (!Roles.Contains(role, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(Lec5Model.Role), "Vui lòng chọn quyền hợp lệ.");
    }
}