# Hướng dẫn chung cho GitHub Copilot – Phần mềm quản lý (ASP.NET Core MVC)

## Bối cảnh dự án
- Phần mềm quản lý (ví dụ: sinh viên, khoa, ...) xây dựng bằng **ASP.NET Core MVC** + **Entity Framework Core** + **ASP.NET Core Identity**.
- Giao diện: **Bootstrap 5**, CRUD dùng **Modal + Ajax**, có **phân trang & tìm kiếm** trên mọi trang danh sách.
- Có **phân quyền động** theo Role cho từng action, **nhập dữ liệu từ Excel**, **sinh mã tự động**, **gửi email** (xác thực tài khoản, quên mật khẩu).
- Trả lời, comment, thông báo cho người dùng bằng **tiếng Việt**. Tên class/method/biến dùng **tiếng Anh** (PascalCase/camelCase).

## Quy ước kiến trúc
- Controller mỏng: chỉ nhận request, validate `ModelState`, gọi service, trả kết quả. Logic nghiệp vụ đặt trong `Services/`.
- Không truy cập `DbContext` trực tiếp trong View. Controller có thể dùng `DbContext` cho truy vấn đơn giản, logic phức tạp phải tách service.
- Dùng **ViewModel** cho form/ danh sách khi dữ liệu khác Entity; không bind trực tiếp Entity có trường nhạy cảm (chống over-posting).
- Mọi action `POST/PUT/DELETE` phải có `[HttpPost]` (hoặc tương ứng) và `[ValidateAntiForgeryToken]`.
- Truy vấn bất đồng bộ (`async/await`, `ToListAsync`, `FirstOrDefaultAsync`). Dùng `AsNoTracking()` cho truy vấn chỉ đọc.
- Phân trang luôn thực hiện ở DB (`Skip/Take`), không tải hết rồi cắt trong bộ nhớ.

## Bảo mật
- KHÔNG hard-code connection string, mật khẩu SMTP, mật khẩu admin trong code. Dùng User Secrets / biến môi trường.
- Mọi controller mặc định yêu cầu đăng nhập (`[Authorize]`); chỉ mở `[AllowAnonymous]` khi cần.
- Không tắt kiểm tra chứng chỉ, không log dữ liệu nhạy cảm.

## Cơ sở dữ liệu
- Thay đổi schema luôn qua **EF Core Migrations**; không sửa migration đã áp dụng. Xem skill `ef-core-migrations`.
- Khoá ngoại cấu hình rõ ràng (Fluent API hoặc navigation property), đặt `OnDelete` phù hợp.

## Giao diện
- Tách CSS vào `wwwroot/css/site.css`, tham chiếu trong `_Layout.cshtml`. Không viết style inline trừ trường hợp bất khả kháng.
- Dùng Partial View cho nội dung modal và bảng danh sách.

## Các skill có sẵn (`.github/skills/`)
| Skill | Dùng khi |
|---|---|
| mvc-crud-ajax-modal | Tạo/sửa chức năng CRUD dùng Modal + Ajax |
| pagination-search | Thêm phân trang và tìm kiếm cho trang danh sách |
| ef-core-migrations | Tạo entity, quan hệ khoá ngoại, migration, seed dữ liệu |
| auto-code-generation | Sinh mã tự động (vd: mã sinh viên 2821050199) |
| dynamic-authorization | Phân quyền động Role → Controller/Action |
| excel-import | Nhập dữ liệu từ Excel (ghi đè / bổ sung) |
| identity-email | Identity, tài khoản mặc định, gửi email xác thực / quên mật khẩu |
| project-audit | Kiểm tra dự án đã đủ chức năng theo yêu cầu chưa |
