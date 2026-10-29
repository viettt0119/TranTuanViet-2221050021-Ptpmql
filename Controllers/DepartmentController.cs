using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

public class DepartmentController : Controller
{
    private readonly ApplicationDbContext _context;
    public DepartmentController(ApplicationDbContext context) { _context = context; }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["ModalTitle"] = "Thêm Khoa";
        ViewData["IsCreate"] = true;
        ViewData["FormActionUrl"] = Url.Action(nameof(Create));
        return PartialView("_FormModal", new DepartmentModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("DepartmentCode,DepartmentName")] DepartmentModel department)
    {
        if (await _context.Departments.AnyAsync(d => d.DepartmentCode == department.DepartmentCode))
            ModelState.AddModelError(nameof(department.DepartmentCode), "Mã Khoa đã tồn tại.");
        if (ModelState.IsValid)
        {
            _context.Add(department);
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã thêm Khoa." });
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Không thể lưu Khoa. Kiểm tra mã có bị trùng không.");
            }
        }
        return FormModal(department, nameof(Create), "Thêm Khoa", true);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();
        ViewData["ModalTitle"] = "Sửa Khoa";
        ViewData["IsCreate"] = false;
        ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id });
        return PartialView("_FormModal", department);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("DepartmentCode,DepartmentName")] DepartmentModel form)
    {
        if (id != form.DepartmentCode) return BadRequest();
        if (!ModelState.IsValid)
            return FormModal(form, nameof(Edit), "Sửa Khoa", false, new { id });

        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();
        department.DepartmentName = form.DepartmentName;
        try
        {
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã cập nhật tên Khoa." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể cập nhật Khoa.");
            return FormModal(form, nameof(Edit), "Sửa Khoa", false, new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var department = await _context.Departments.Include(d => d.Students)
            .FirstOrDefaultAsync(d => d.DepartmentCode == id);
        if (department == null) return NotFound();
        return PartialView("_DetailsModal", department);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();
        ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
        return PartialView("_DeleteModal", department);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();
        if (await _context.Students.AnyAsync(s => s.DepartmentCode == id))
        {
            ModelState.AddModelError("", "Không thể xóa Khoa đang có sinh viên. Hãy chuyển sinh viên sang Khoa khác trước.");
            return DeleteModal(department, id);
        }

        _context.Remove(department);
        try
        {
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa Khoa." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể xóa Khoa; có thể vừa có sinh viên được thêm vào.");
            return DeleteModal(department, id);
        }
    }

    private PartialViewResult FormModal(
        DepartmentModel department,
        string action,
        string title,
        bool isCreate,
        object? routeValues = null)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        ViewData["ModalTitle"] = title;
        ViewData["IsCreate"] = isCreate;
        ViewData["FormActionUrl"] = Url.Action(action, routeValues);
        return PartialView("_FormModal", department);
    }

    private PartialViewResult DeleteModal(DepartmentModel department, string id)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
        return PartialView("_DeleteModal", department);
    }
}
