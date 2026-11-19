using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Services;

namespace DemoMVC.Controllers;

public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly StudentCodeGenerator _codeGenerator;

    public StudentController(ApplicationDbContext context, StudentCodeGenerator codeGenerator)
    {
        _context = context;
        _codeGenerator = codeGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        var studentsQuery = _context.Students.Include(s => s.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLowerInvariant();
            studentsQuery = studentsQuery.Where(s =>
                s.StudentCode.ToLower().Contains(search) ||
                s.FullName.ToLower().Contains(search) ||
                s.DepartmentCode.ToLower().Contains(search) ||
                s.Department!.DepartmentName.ToLower().Contains(search) ||
                s.University.ToLower().Contains(search));
        }

        var students = await PagedList<StudentModel>.CreateAsync(
            studentsQuery.OrderBy(s => s.StudentCode), page, pageSize);
        ViewData["SearchQuery"] = q?.Trim();
        return View(students);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var student = await _context.Students.Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();
        return PartialView("_DetailsModal", student);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDepartmentsAsync();
        ViewData["ModalTitle"] = "Thêm sinh viên";
        ViewData["FormActionUrl"] = Url.Action(nameof(Create));
        ViewData["IsCreate"] = true;
        return PartialView("_FormModal", new StudentModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("FullName,Address,University,DepartmentCode,AdmissionYear,TrainingSystemCode,MajorCode")]
        StudentModel student)
    {
        // Form không nhập mã; controller sẽ tự cấp mã sau khi kiểm tra dữ liệu.
        ModelState.Remove(nameof(StudentModel.StudentCode));
        await ValidateDepartmentAsync(student.DepartmentCode);
        if (ModelState.IsValid)
        {
            try
            {
                await _codeGenerator.CreateAsync(student);
                return Json(new { success = true, message = $"Đã thêm sinh viên {student.StudentCode}." });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Không thể lưu sinh viên. Kiểm tra Khoa và bộ đếm mã, rồi thử lại.");
            }
            catch (SqliteException)
            {
                ModelState.AddModelError("", "Database chưa sẵn sàng hoặc đang bận. Kiểm tra cấu trúc database và thử lại.");
            }
        }
        // Form lỗi vẫn phải nạp lại danh sách Khoa.
        return await FormModalAsync(student, nameof(Create), "Thêm sinh viên", true);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var student = await _context.Students.FindAsync(id);
        if (student == null) return NotFound();
        await LoadDepartmentsAsync(student.DepartmentCode);
        ViewData["ModalTitle"] = "Sửa sinh viên";
        ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id });
        ViewData["IsCreate"] = false;
        return PartialView("_FormModal", student);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id)
    {
        // Lấy bản ghi thật; chỉ nhận các trường được phép sửa từ form.
        var student = await _context.Students.FindAsync(id);
        if (student == null) return NotFound();
        bool updated = await TryUpdateModelAsync(student, "",
            s => s.FullName, s => s.Address, s => s.University, s => s.DepartmentCode);
        await ValidateDepartmentAsync(student.DepartmentCode);
        if (updated && ModelState.IsValid)
        {
            try
            {
                // Mã, năm nhập học, hệ và ngành đã cấp không thay đổi.
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật sinh viên." });
            }
            catch (DbUpdateConcurrencyException) { return NotFound(); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu thay đổi. Vui lòng thử lại."); }
        }
        return await FormModalAsync(student, nameof(Edit), "Sửa sinh viên", false, new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var student = await _context.Students.Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();
        ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
        return PartialView("_DeleteModal", student);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _context.Students.Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();
        _context.Students.Remove(student);
        try
        {
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa sinh viên. Số thứ tự đã cấp không được dùng lại." });
        }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể xóa sinh viên. Vui lòng thử lại.");
            return DeleteModal(student, id);
        }
    }

    private async Task<IActionResult> FormModalAsync(
        StudentModel student,
        string action,
        string title,
        bool isCreate,
        object? routeValues = null)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await LoadDepartmentsAsync(student.DepartmentCode);
        ViewData["ModalTitle"] = title;
        ViewData["FormActionUrl"] = Url.Action(action, routeValues);
        ViewData["IsCreate"] = isCreate;
        return PartialView("_FormModal", student);
    }

    private PartialViewResult DeleteModal(StudentModel student, int id)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
        return PartialView("_DeleteModal", student);
    }

    private async Task LoadDepartmentsAsync(string? selectedCode = null)
    {
        var departments = await _context.Set<DepartmentModel>()
            .OrderBy(d => d.DepartmentName).ToListAsync();
        ViewBag.DepartmentCode = new SelectList(departments,
            "DepartmentCode", "DepartmentName", selectedCode);
        ViewBag.HasDepartments = departments.Count > 0;
    }

    private async Task ValidateDepartmentAsync(string departmentCode)
    {
        bool exists = await _context.Set<DepartmentModel>()
            .AnyAsync(d => d.DepartmentCode == departmentCode);
        if (!exists)
            ModelState.AddModelError(nameof(StudentModel.DepartmentCode), "Vui lòng chọn Khoa đang tồn tại.");
    }

}
