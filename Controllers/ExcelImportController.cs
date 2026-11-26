using DemoMVC.Models;
using DemoMVC.Services;
using OfficeOpenXml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace DemoMVC.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
[PermissionModule("Nhập Excel", "Hệ thống", 50)]
public sealed class ExcelImportController(
    ExcelImportService importService,
    IMemoryCache cache,
    PermissionCatalogService permissionCatalog) : Controller
{
    private const string RolePermissionEntity = "rolepermission";
    private static readonly object PreviewLock = new();
    private static string CacheKey(string token) => $"excel-preview:{token}";

    private async Task<bool> CanImportAsync(string entity)
    {
        if (!ExcelImportService.IsSupportedEntity(entity)) return false;
        if (User.IsInRole("Admin")) return true;
        if (entity == RolePermissionEntity) return false;
        var module = EntityController(entity);
        return await permissionCatalog.HasAccessAsync(User, $"{module}.Create") ||
            await permissionCatalog.HasAccessAsync(User, $"{module}.Edit");
    }

    private async Task<bool> CanExecuteAsync(ExcelImportPreview preview, ExcelImportMode mode)
    {
        if (!Enum.IsDefined(mode) || !await CanImportAsync(preview.Entity)) return false;
        if (User.IsInRole("Admin")) return true;
        var module = EntityController(preview.Entity);
        if ((mode == ExcelImportMode.Append || preview.NewCount > 0) &&
            !await permissionCatalog.HasAccessAsync(User, $"{module}.Create")) return false;
        return mode != ExcelImportMode.Overwrite || preview.DuplicateCount == 0 ||
            await permissionCatalog.HasAccessAsync(User, $"{module}.Edit");
    }

    private static string EntityController(string entity) => entity switch
    {
        "student" => "Student", "department" => "Department", "employee" => "Employee", "product" => "Product", _ => ""
    };

    [HttpGet]
    [PermissionAction("Xem trang nhập")]
    public Task<IActionResult> Index(string? entity = null) => UploadViewAsync(entity);

    // Trang tải file chỉ liệt kê loại dữ liệu mà người dùng được phép thêm hoặc sửa.
    private async Task<IActionResult> UploadViewAsync(string? selectedEntity)
    {
        var allowed = new List<string>();
        foreach (var item in ExcelImportService.SupportedEntities)
        {
            if (await CanImportAsync(item))
                allowed.Add(item);
        }

        ViewData["AllowedEntities"] = allowed;
        ViewData["SelectedEntity"] = selectedEntity?.Trim().ToLowerInvariant();
        return View("Index", new ExcelImportResult());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAction("Tải file lên, xem trước")]
    public async Task<IActionResult> Import(IFormFile? file, string entity = "department", bool overwrite = false)
    {
        entity = entity?.Trim().ToLowerInvariant() ?? "";
        if (!ExcelImportService.IsSupportedEntity(entity)) return BadRequest("Loại dữ liệu không được hỗ trợ.");
        if (!await CanImportAsync(entity)) return Forbid();
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(ownerId)) return Challenge();
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("", "Vui lòng chọn file Excel.");
            return await UploadViewAsync(entity);
        }
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Chỉ hỗ trợ file .xlsx.");
            return await UploadViewAsync(entity);
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("", "File Excel không được vượt quá 5 MB.");
            return await UploadViewAsync(entity);
        }
        await using var stream = file.OpenReadStream();
        ExcelImportPreview preview;
        try
        {
            preview = await importService.PreviewAsync(stream, entity, HttpContext.RequestAborted);
        }
        // EPPlus ném InvalidDataException khi file hỏng, bị mã hoá hoặc chỉ được đổi đuôi thành .xlsx.
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("", "Không đọc được file Excel. Hãy dùng file .xlsx hợp lệ, không mã hoá, theo mẫu được cung cấp.");
            return await UploadViewAsync(entity);
        }
        preview.OwnerUserId = ownerId;
        preview.CanAppend = await CanExecuteAsync(preview, ExcelImportMode.Append);
        preview.CanOverwrite = await CanExecuteAsync(preview, ExcelImportMode.Overwrite);
        preview.Token = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(preview.Token), preview, TimeSpan.FromMinutes(30));
        ViewData["Entity"] = entity;
        ViewData["Preview"] = preview;
        return View("Preview", preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAction("Xác nhận nhập dữ liệu")]
    public async Task<IActionResult> Confirm(string previewToken, ExcelImportMode mode)
    {
        if (!Enum.IsDefined(mode) || string.IsNullOrWhiteSpace(previewToken))
            return BadRequest("Chế độ nhập hoặc phiên xem trước không hợp lệ.");
        if (!cache.TryGetValue(CacheKey(previewToken), out ExcelImportPreview? preview) || preview is null)
        {
            ModelState.AddModelError("", "Phiên xem trước đã hết hạn. Vui lòng tải file lên lại.");
            return await UploadViewAsync(null);
        }

        if (preview.OwnerUserId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Forbid();
        if (!await CanExecuteAsync(preview, mode)) return Forbid();
        if (preview.Rows.Count == 0)
        {
            ModelState.AddModelError("", "File không có dòng hợp lệ để nhập.");
            return await UploadViewAsync(preview.Entity);
        }

        // Lấy và xoá một lần trước khi chờ I/O: hai POST đồng thời không thể cùng thực thi một preview.
        lock (PreviewLock)
        {
            if (!cache.TryGetValue(CacheKey(previewToken), out ExcelImportPreview? current) || !ReferenceEquals(current, preview))
                return Conflict("Phiên xem trước đã được sử dụng. Vui lòng tải file lên lại.");
            cache.Remove(CacheKey(previewToken));
        }

        try
        {
            var result = await importService.ExecuteAsync(preview, mode, HttpContext.RequestAborted);
            if (preview.Entity == RolePermissionEntity)
                permissionCatalog.InvalidateAll();
            return View("Result", result);
        }
        catch (InvalidOperationException ex)
        {
            var result = new ExcelImportResult { ErrorCount = preview.Errors.Count + preview.Rows.Count };
            result.Errors.AddRange(preview.Errors.Select(error => $"Dòng {error.Row}: {error.Message}"));
            result.Errors.Add(ex.Message);
            result.Errors.Add("Không lưu dòng nào trong lần xác nhận này. Hãy sửa dữ liệu và tải file lên lại.");
            return View("Result", result);
        }
    }

    [HttpGet]
    [PermissionAction("Tải file mẫu")]
    public async Task<IActionResult> DownloadTemplate(string entity = "department")
    {
        entity = entity?.Trim().ToLowerInvariant() ?? "";
        if (!ExcelImportService.IsSupportedEntity(entity)) return BadRequest("Loại dữ liệu không được hỗ trợ.");
        if (!await CanImportAsync(entity)) return Forbid();
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Data");
        (string[] Headers, object[] Example) template = entity.ToLowerInvariant() switch
        {
            "employee" => (["EmpId", "FullName", "HireDate"], ["NV001", "Nguyen Van A", "2026-11-19"]),
            "product" => (["Id", "Name", "Price"], [1, "San pham mau", 1000]),
            "rolepermission" => (["RoleName", "Permission", "IsGranted"], ["User", "Student.Index", 1]),
            "student" => (
                ["StudentCode", "FullName", "Address", "University", "DepartmentCode", "AdmissionYear", "TrainingSystemCode", "MajorCode", "Email", "PhoneNumber", "DateOfBirth", "Gender"],
                ["", "Nguyen Van A", "Ha Noi", "Đại học Mỏ - Địa chất", "CNTT", 2026, "21", "05", "student@example.com", "0901234567", "2006-01-15", "Nam"]),
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
