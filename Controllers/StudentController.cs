using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Models.ViewModels.Students;
using DemoMVC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace DemoMVC.Controllers;

[PermissionModule("Sinh viên", "Đào tạo", 10)]
public class StudentController(
    ApplicationDbContext context,
    StudentService studentService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StudentFilterViewModel filter)
    {
        var model = await studentService.GetPagedAsync(filter, HttpContext.RequestAborted);
        ViewData["SearchQuery"] = filter.Q?.Trim();

        if (IsAjaxRequest())
            return PartialView("_StudentTable", model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var student = await studentService.GetDetailsAsync(id.Value, HttpContext.RequestAborted);
        if (student is null) return NotFound();
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        return PartialView("_DetailsModal", student);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        var departments = await context.Departments.AsNoTracking()
            .OrderBy(x => x.DepartmentName).ToListAsync(HttpContext.RequestAborted);
        ViewBag.DepartmentCode = new SelectList(departments, "DepartmentCode", "DepartmentName");
        ViewBag.HasDepartments = departments.Count > 0;
        ViewData["ModalTitle"] = "Thêm sinh viên";
        ViewData["FormActionUrl"] = Url.Action(nameof(Create));
        ViewData["IsCreate"] = true;
        return PartialView("_FormModal", new StudentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel form)
    {
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        if (!ModelState.IsValid)
            return await FormValidationAsync(form, true);

        var result = await studentService.CreateAsync(form, HttpContext.RequestAborted);
        if (!result.Success)
        {
            foreach (var error in result.FieldErrors)
                ModelState.AddModelError(error.Key, error.Value);
            if (result.FieldErrors.Count == 0)
                ModelState.AddModelError(string.Empty, result.Message ?? "Không thể thêm sinh viên.");
            return await FormValidationAsync(form, true);
        }
        return Json(new { success = true, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var student = await studentService.GetForEditAsync(id.Value, HttpContext.RequestAborted);
        if (student is null) return NotFound();
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        return await EditModalAsync(student);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentFormViewModel form)
    {
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        if (!ModelState.IsValid)
            return await FormValidationAsync(form, false);
        var result = await studentService.UpdateAsync(id, form, HttpContext.RequestAborted);
        if (!result.Success)
        {
            foreach (var error in result.FieldErrors)
                ModelState.AddModelError(error.Key, error.Value);
            if (result.FieldErrors.Count == 0)
                ModelState.AddModelError(string.Empty, result.Message ?? "Không thể cập nhật sinh viên.");
            return await FormValidationAsync(form, false);
        }
        return Json(new { success = true, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var student = await studentService.GetDetailsAsync(id.Value, HttpContext.RequestAborted);
        if (student is null) return NotFound();
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        ViewData["FormActionUrl"] = Url.Action(nameof(DeleteConfirmed), new { id });
        return PartialView("_DeleteModal", student);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, Guid concurrencyStamp)
    {
        if (!IsAjaxRequest()) return RedirectToAction(nameof(Index));
        var result = await studentService.DeleteAsync(id, concurrencyStamp, HttpContext.RequestAborted);
        if (!result.Success)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            var student = await studentService.GetDetailsAsync(id, HttpContext.RequestAborted);
            if (student is null) return NotFound();
            ViewData["FormActionUrl"] = Url.Action(nameof(DeleteConfirmed), new { id });
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể xoá sinh viên.");
            return PartialView("_DeleteModal", student);
        }
        return Json(new { success = true, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] StudentFilterViewModel filter)
    {
        var query = context.Students.AsNoTracking().Include(student => student.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var keyword = $"%{filter.Q.Trim()}%";
            query = query.Where(student =>
                EF.Functions.Like(student.StudentCode, keyword) ||
                EF.Functions.Like(student.FullName, keyword) ||
                (student.Email != null && EF.Functions.Like(student.Email, keyword)));
        }
        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
            query = query.Where(student => student.DepartmentCode == filter.DepartmentCode);
        if (filter.AdmissionYear.HasValue)
            query = query.Where(student => student.AdmissionYear == filter.AdmissionYear.Value);
        var students = await query.OrderBy(student => student.StudentCode).ToListAsync(HttpContext.RequestAborted);

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("SinhVien");
        var headers = new[] { "Mã SV", "Họ tên", "Email", "Số điện thoại", "Khoa", "Năm", "Trường", "Ngày sinh" };
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cells[1, column + 1].Value = headers[column];
            sheet.Cells[1, column + 1].Style.Font.Bold = true;
        }
        for (var index = 0; index < students.Count; index++)
        {
            var student = students[index];
            var row = index + 2;
            sheet.Cells[row, 1].Value = student.StudentCode;
            sheet.Cells[row, 2].Value = student.FullName;
            sheet.Cells[row, 3].Value = student.Email;
            sheet.Cells[row, 4].Value = student.PhoneNumber;
            sheet.Cells[row, 5].Value = student.Department?.DepartmentName;
            sheet.Cells[row, 6].Value = student.AdmissionYear;
            sheet.Cells[row, 7].Value = student.University;
            sheet.Cells[row, 8].Value = student.DateOfBirth?.ToString("yyyy-MM-dd");
        }
        sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
        sheet.Cells.AutoFilter = true;
        var bytes = package.GetAsByteArray();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"SinhVien_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    private async Task<IActionResult> EditModalAsync(StudentFormViewModel student)
    {
        var departments = await context.Departments.AsNoTracking()
            .OrderBy(x => x.DepartmentName).ToListAsync(HttpContext.RequestAborted);
        ViewBag.DepartmentCode = new SelectList(departments, "DepartmentCode", "DepartmentName", student.DepartmentCode);
        ViewBag.HasDepartments = departments.Count > 0;
        ViewData["ModalTitle"] = "Sửa sinh viên";
        ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id = student.Id });
        ViewData["IsCreate"] = false;
        return PartialView("_FormModal", student);
    }

    private async Task<IActionResult> FormValidationAsync(StudentFormViewModel form, bool isCreate)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        var departments = await context.Departments.AsNoTracking()
            .OrderBy(x => x.DepartmentName).ToListAsync(HttpContext.RequestAborted);
        ViewBag.DepartmentCode = new SelectList(departments, "DepartmentCode", "DepartmentName", form.DepartmentCode);
        ViewBag.HasDepartments = departments.Count > 0;
        ViewData["ModalTitle"] = isCreate ? "Thêm sinh viên" : "Sửa sinh viên";
        ViewData["FormActionUrl"] = isCreate
            ? Url.Action(nameof(Create))
            : Url.Action(nameof(Edit), new { id = form.Id });
        ViewData["IsCreate"] = isCreate;
        return PartialView("_FormModal", form);
    }

    private bool IsAjaxRequest() =>
        Request.Headers.TryGetValue("X-Requested-With", out var value)
        && value.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
