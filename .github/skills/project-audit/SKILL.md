---
name: project-audit
description: Kiểm tra dự án ASP.NET Core MVC đã thực hiện đầy đủ các hạng mục theo lộ trình thực tập chưa (môi trường, CI/CD, routing, MVC, layout, CRUD, Ajax, phân trang, Identity, phân quyền, import Excel) và xuất báo cáo.
---

# Skill: Audit dự án

**Chỉ đọc và báo cáo, không sửa code.** Mỗi kết luận phải kèm bằng chứng (file, class/method). Không tìm thấy → ghi "Chưa tìm thấy".

## Trạng thái
✅ Đạt | ⚠️ Làm một phần | ❌ Chưa làm | ❓ Cần xác nhận thủ công

## Danh mục kiểm tra
1. **Môi trường**: .NET SDK (`global.json`, TargetFramework), `.vscode/extensions.json`, README hướng dẫn.
2. **CI/CD**: `.github/workflows/*.yml` có build → publish → deploy tự động; hosting/domain (cấu hình publish, URL trong README).
3. **Kiến trúc MVC**: Models/Views/Controllers tách bạch, controller mỏng.
4. **Routing**: route mặc định, trang lỗi 404/500 (`UseStatusCodePagesWithReExecute`, `UseExceptionHandler`), `[Route]`/`[HttpGet("...")]`.
5. **Truyền dữ liệu M-V-C**: ví dụ Controller→View trực tiếp và qua Model (`@model`).
6. **Trạng thái**: `ViewBag`, `ViewData`, `TempData` có ví dụ dùng đúng.
7. **Layout**: Bootstrap 5, CSS tách file và gắn trong `_Layout`, không lẫn cú pháp Bootstrap 3/4.
8. **CSDL & CRUD**: connection string, DbContext, Migrations đầy đủ và không lệch model; CRUD từng entity.
9. **Khoá ngoại + seed Khoa**; form dùng dropdown, hiển thị tên liên kết.
10. **Sinh mã tự động** (mã SV 10 số): logic, tăng đúng, chống trùng, tràn 9999.
11. **Ajax/Modal**: bảng entity × (Create/Edit/Details/Delete) cái nào đã dùng Modal+Ajax.
12. **Phân trang & tìm kiếm**: bảng trang × (phân trang, tìm kiếm); phân trang ở DB.
13. **Identity**: tích hợp, tài khoản mặc định, gửi email xác thực/quên mật khẩu, bí mật không lộ trong repo.
14. **Role & phân quyền động**: CRUD Role, gán Role, ma trận quyền, mọi action đều qua kiểm tra quyền.
15. **Import Excel**: bảng entity × (có import, có file mẫu); xác nhận ghi đè/bổ sung; validate + transaction.
16. **Chất lượng chung**: `dotnet build` sạch, anti-forgery trên POST, `.gitignore`, không commit secrets.

## Cách làm
- Chạy: `dotnet build`, `dotnet ef migrations list`, `dotnet ef migrations has-pending-model-changes`, `dotnet test` (nếu có).
- Liệt kê toàn bộ controller/action bằng tìm kiếm mã nguồn để lập các bảng đối chiếu.

## Báo cáo (`BAO_CAO_KIEM_TRA.md`)
1. Bảng tổng quan 16 mục + % hoàn thành.
2. Chi tiết từng mục: trạng thái, bằng chứng, phần thiếu, đề xuất.
3. Các bảng đối chiếu (CRUD, Ajax, phân trang/tìm kiếm, import, phân quyền).
4. Vấn đề bảo mật/chất lượng phát hiện thêm.
5. Việc cần làm theo mức ưu tiên (Cao/Trung bình/Thấp).
