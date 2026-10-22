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
        return View(await _context.Set<DepartmentModel>().OrderBy(d => d.DepartmentName).ToListAsync());
    }

    [HttpGet]
    public IActionResult Create() { return View(new DepartmentModel()); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("DepartmentCode,DepartmentName")] DepartmentModel department)
    {
        if (await _context.Set<DepartmentModel>().AnyAsync(d => d.DepartmentCode == department.DepartmentCode))
            ModelState.AddModelError(nameof(department.DepartmentCode), "Mã Khoa đã tồn tại.");
        if (ModelState.IsValid)
        {
            _context.Add(department);
            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm Khoa.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu Khoa. Kiểm tra mã có bị trùng không."); }
        }
        return View(department);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var department = await _context.Set<DepartmentModel>().FindAsync(id);
        if (department == null) return NotFound();
        return View(department);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("DepartmentCode,DepartmentName")] DepartmentModel form)
    {
        if (id != form.DepartmentCode) return BadRequest();
        var department = await _context.Set<DepartmentModel>().FindAsync(id);
        if (department == null) return NotFound();
        if (ModelState.IsValid)
        {
            department.DepartmentName = form.DepartmentName;
            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật tên Khoa.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException) { return NotFound(); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Không thể cập nhật Khoa."); }
        }
        return View(form);
    }

    public async Task<IActionResult> Details(string id)
    {
        var department = await _context.Set<DepartmentModel>().Include(d => d.Students)
            .FirstOrDefaultAsync(d => d.DepartmentCode == id);
        if (department == null) return NotFound();
        return View(department);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var department = await _context.Set<DepartmentModel>().FindAsync(id);
        if (department == null) return NotFound();
        return View(department);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var department = await _context.Set<DepartmentModel>().FindAsync(id);
        if (department == null) return NotFound();
        if (await _context.Students.AnyAsync(s => s.DepartmentCode == id))
        {
            ModelState.AddModelError("", "Không thể xóa Khoa đang có sinh viên. Hãy chuyển sinh viên sang Khoa khác trước.");
            return View("Delete", department);
        }
        _context.Remove(department);
        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa Khoa.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể xóa Khoa; có thể vừa có sinh viên được thêm vào.");
            return View("Delete", department);
        }
    }
}
