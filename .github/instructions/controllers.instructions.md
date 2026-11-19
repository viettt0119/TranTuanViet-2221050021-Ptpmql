---
applyTo: "**/Controllers/**/*.cs"
---
- Kế thừa `Controller`, đặt `[Authorize]` ở cấp class (trừ controller công khai như Account/Home).
- Action ghi dữ liệu: `[HttpPost]` + `[ValidateAntiForgeryToken]`.
- Action phục vụ Ajax: trả `PartialView(...)` cho form modal, `Json(new { success, message })` cho kết quả lưu.
- Luôn kiểm tra `ModelState.IsValid`; nếu lỗi với Ajax trả lại partial form kèm lỗi (status 200 + `success=false`, hoặc 400 theo thống nhất của dự án).
- Không `catch (Exception)` rồi bỏ qua; log bằng `ILogger<T>` và trả thông báo thân thiện bằng tiếng Việt.
- Không nhận Entity trực tiếp từ form nếu có trường không được phép sửa; dùng ViewModel hoặc `[Bind]`/`TryUpdateModelAsync`.
- Tham số `id` kiểm tra null/not found → `NotFound()`.
