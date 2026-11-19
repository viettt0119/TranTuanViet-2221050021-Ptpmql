---
name: excel-import
description: Nhập dữ liệu từ file Excel vào các bảng (trừ bảng Identity) với xem trước, kiểm tra lỗi từng dòng, lựa chọn ghi đè hoặc ghi bổ sung, và transaction. Dùng khi thêm chức năng import cho một entity.
---

# Skill: Import Excel

## Thư viện
- Ưu tiên **ClosedXML** (MIT) hoặc NPOI. Nếu dùng **EPPlus 5+** cần lưu ý giấy phép thương mại (đặt `ExcelPackage.LicenseContext` đúng bản dùng phi thương mại).

## Luồng 4 bước (mỗi entity một nút "Nhập Excel")
1. **Tải file mẫu**: action `DownloadTemplate` sinh file .xlsx có đúng tiêu đề cột, dòng ví dụ, ghi chú định dạng.
2. **Upload & kiểm tra (chưa ghi DB)**: giới hạn `.xlsx`, tối đa ~5MB; đọc file, validate từng dòng, trả về **bản xem trước**: tổng dòng, dòng hợp lệ, dòng lỗi (số dòng + lý do), số dòng *sẽ thêm mới* và số dòng *trùng khoá*.
3. **Xác nhận chế độ** khi có dữ liệu trùng:
   - **Ghi đè**: cập nhật bản ghi đã có theo khoá.
   - **Ghi bổ sung**: chỉ thêm bản ghi mới, bỏ qua bản ghi trùng.
   - Hộp thoại xác nhận nêu rõ số lượng bị ảnh hưởng; người dùng có thể Huỷ.
4. **Thực thi trong transaction**: tất cả hoặc không gì cả; báo kết quả (thêm x, cập nhật y, bỏ qua z).

## Khung code
```csharp
public interface IExcelImporter<TEntity>
{
    Task<ImportPreview> PreviewAsync(Stream file, CancellationToken ct);
    Task<ImportResult> ExecuteAsync(string previewToken, ImportMode mode, CancellationToken ct);
}
public enum ImportMode { Append, Overwrite }

public record ImportRowError(int Row, string Message);
public class ImportPreview
{
    public string Token { get; set; } = null!;           // khoá lưu dữ liệu tạm (cache/session/temp file)
    public int Total { get; set; }
    public int Valid { get; set; }
    public int NewCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}
```
Thực thi:
```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
try { /* thêm/cập nhật theo mode */ await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
catch { await tx.RollbackAsync(ct); throw; }
```

## Validate tối thiểu
- Đúng dòng tiêu đề; thiếu cột bắt buộc → báo lỗi ngay.
- Kiểu dữ liệu (ngày, số), độ dài, trường bắt buộc, trùng khoá **ngay trong file**.
- **Khoá ngoại tồn tại** (vd MaKhoa phải có trong bảng Khoa) — nạp danh mục vào `HashSet` một lần để tra nhanh.
- Mã tự sinh (vd mã sinh viên): sinh trong lúc thực thi, theo dõi số đã cấp để không trùng giữa các dòng (xem skill `auto-code-generation`).
- Chống công thức/CSV injection: loại bỏ ký tự đầu `= + - @` khi cần xuất lại.

## Thứ tự import khi bảng có phụ thuộc
Danh mục trước (Khoa, Hệ, Ngành) → bảng con sau (Sinh viên, ...).

## Quy tắc
1. Áp dụng cho **mọi bảng dữ liệu nghiệp vụ**, **trừ** `AspNetUsers/Roles/...` của Identity.
2. Đặt quyền riêng `X.Import` trong phân quyền động.
3. Không tin tên file/MIME từ client; kiểm tra phần mở rộng và mở thử bằng thư viện.
4. File lớn: đọc dạng stream, xử lý theo lô (batch 500–1000 dòng).
