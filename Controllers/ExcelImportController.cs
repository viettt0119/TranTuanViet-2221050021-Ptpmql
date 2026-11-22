using DemoMVC.Models;
using DemoMVC.Services;
using OfficeOpenXml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DemoMVC.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class ExcelImportController(
    ExcelImportService importService,
    IMemoryCache cache) : Controller
{
    [HttpGet]
    [RequirePermission(Permissions.ExcelImport.View)]
    public IActionResult Index() => View(new ExcelImportResult());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.ExcelImport.Import)]
    public async Task<IActionResult> Import(IFormFile? file, string entity = "department", bool overwrite = false)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("", "Vui lòng chọn file Excel.");
            return View("Index", new ExcelImportResult());
        }
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Chỉ hỗ trợ file .xlsx.");
            return View("Index", new ExcelImportResult());
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("", "File Excel không được vượt quá 5 MB.");
            return View("Index", new ExcelImportResult());
        }
        await using var stream = file.OpenReadStream();
        var preview = await importService.PreviewAsync(stream, entity);
        if (preview.Errors.Count > 0)
        {
            foreach (var error in preview.Errors)
                ModelState.AddModelError("", $"Dòng {error.Row}: {error.Message}");
        }
        preview.Token = Guid.NewGuid().ToString("N");
        cache.Set(preview.Token, preview, TimeSpan.FromMinutes(30));
        ViewData["Entity"] = entity;
        ViewData["Preview"] = preview;
        return View("Preview", preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.ExcelImport.Import)]
    public async Task<IActionResult> Confirm(string previewToken, ExcelImportMode mode)
    {
        if (!cache.TryGetValue(previewToken, out ExcelImportPreview? preview) || preview is null)
        {
            ModelState.AddModelError("", "Phiên xem trước đã hết hạn. Vui lòng tải file lên lại.");
            return View("Index", new ExcelImportResult());
        }

        try
        {
            var result = await importService.ExecuteAsync(preview, mode);
            cache.Remove(previewToken);
            return View("Result", result);
        }
        catch (InvalidOperationException ex)
        {
            cache.Remove(previewToken);
            var result = new ExcelImportResult { ErrorCount = 1 };
            result.Errors.Add(ex.Message);
            return View("Result", result);
        }
    }

    [HttpGet]
    [RequirePermission(Permissions.ExcelImport.View)]
    public IActionResult DownloadTemplate(string entity = "department")
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Data");
        (string[] Headers, object[] Example) template = entity.ToLowerInvariant() switch
        {
            "employee" => (["EmpId", "FullName", "HireDate"], ["NV001", "Nguyen Van A", "2026-11-19"]),
            "product" => (["Id", "Name", "Price"], [1, "San pham mau", 1000]),
            "student" => (
                ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode"],
                ["", "Nguyen Van A", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", 2026, "21", "05"]),
            _ => (["DepartmentCode", "DepartmentName"], ["CNTT", "Công nghệ thông tin"])
        };
        for (var column = 1; column <= template.Headers.Length; column++)
        {
            sheet.Cells[1, column].Value = template.Headers[column - 1];
            sheet.Cells[1, column].Style.Font.Bold = true;
            sheet.Cells[2, column].Value = template.Example[column - 1];
        }
        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Template-{entity}.xlsx");
    }
}
