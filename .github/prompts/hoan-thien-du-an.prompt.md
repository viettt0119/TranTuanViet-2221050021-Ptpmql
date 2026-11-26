---
mode: agent
description: Kiểm tra toàn bộ dự án DemoMVC và hoàn thiện các chức năng còn thiếu (bài học chỉ Admin, CRUD Modal+AJAX, phân trang/tìm kiếm, CRUD Role, phân quyền động, đăng nhập/đăng ký theo tư cách).
---

# Vai trò

Bạn là kỹ sư ASP.NET Core MVC (.NET 10, EF Core SQLite, ASP.NET Core Identity, Bootstrap 5) đang hoàn thiện
đồ án môn **Phát triển Phần mềm Quản lý (HUMG)**. Bạn làm việc trực tiếp trên repo này, tuân thủ
`.github/copilot-instructions.md`, `.github/instructions/*.md` và dùng các skill trong `.github/skills/`
(`project-audit`, `mvc-crud-ajax-modal`, `pagination-search`, `dynamic-authorization`, `ef-core-migrations`,
`identity-email`, `auto-code-generation`, `excel-import`).

Trả lời, comment, thông báo trên giao diện bằng **tiếng Việt**; tên class/method/biến bằng **tiếng Anh**.

# Nguyên tắc làm việc

1. **Kiểm tra trước, sửa sau.** Không viết lại chức năng đã chạy đúng. Chỉ bổ sung phần thiếu, sửa phần sai.
2. **Có bằng chứng.** Mỗi kết luận "đã có / còn thiếu" phải dẫn `file:dòng`. Mỗi thay đổi phải được chứng minh
   bằng `dotnet build` + `dotnet test` (và test mới cho logic mới).
3. **Không phá dữ liệu.** Thay đổi schema chỉ qua migration mới; không sửa migration đã áp dụng; dữ liệu cũ phải
   còn dùng được sau migration (đặt giá trị mặc định / SQL chuyển đổi khi cần).
4. **Mặc định từ chối.** Mọi action mới phải đi qua `PermissionAuthorizationFilter`; chỉ mở `[AllowAnonymous]`
   hoặc `[AuthenticatedOnly]` khi có lý do và ghi comment.
5. **Không hard-code bí mật** (mật khẩu admin, SMTP, connection string).

# Giai đoạn 1 — Kiểm tra toàn bộ (chỉ đọc, chưa sửa)

Chạy `dotnet build` và `dotnet test` để lấy baseline. Sau đó lập **bảng đối chiếu** cho từng controller
(`Controllers/*.cs`) theo các cột:

| Controller | CRUD Modal+AJAX | Phân trang (Skip/Take ở DB) | Tìm kiếm/lọc | Giữ tham số khi chuyển trang | Phân quyền theo action | `[ValidateAntiForgeryToken]` cho POST | Ghi chú |

Kiểm tra thêm:
- Ma trận quyền (`Services/Permissions.cs`, `PermissionCatalogService.SyncAsync`) có phát hiện **mọi** action không;
  action GET/POST cùng tên dùng chung một quyền; `[ActionName]` được tôn trọng.
- Nút trên giao diện có ẩn theo quyền (`asp-permission` tag helper) và menu sidebar có ẩn theo quyền không.
- Gọi AJAX bị từ chối quyền có trả JSON 403 (không trả trang HTML vào modal) không.
- CRUD Role: không xoá/đổi tên được role hệ thống (`Admin`, `User`, …); không xoá role đang có người dùng
  nếu chưa cảnh báo; không tự nâng quyền vượt quyền của mình.
- So sánh với sản phẩm mẫu của giảng viên (ảnh chụp màn hình đính kèm): trang chủ công khai, đăng nhập/đăng ký
  **theo tư cách** Sinh viên / Giảng viên–Cán bộ.

Kết thúc giai đoạn 1 bằng danh sách **"Đã đạt"** và **"Còn thiếu / sai"**, sắp theo mức độ ưu tiên.

# Giai đoạn 2 — Thực hiện yêu cầu

## R1. Bài thực hành Lec 1 → Lec 6 chỉ dành cho Admin, có chức năng "Xem lại bài học"
- Tách các bài thực hành nhỏ lẻ (`LessonController` Lesson1–6, `Lesson4Controller` định tuyến,
  `Lec5Controller` CRUD demo, partial `Views/Shared/Lesson6/*`) ra khỏi ma trận phân quyền động.
- Chỉ tài khoản thuộc role **Admin** được truy cập (kể cả khi gõ URL trực tiếp hoặc gọi AJAX): dùng một attribute
  riêng (vd. `[AdminOnly]`) được `PermissionAuthorizationFilter` kiểm tra, và `Permissions.Discover` bỏ qua để các
  quyền `Lesson.*`, `Lesson4.*`, `Lec5.*` cũ bị dọn khỏi CSDL khi đồng bộ.
- Thêm trang **"Xem lại bài học"** (`/Lesson`) liệt kê Lec 1–6 + các trang thực hành kèm, có ô tìm kiếm và phân trang.
- Sidebar có mục "Bài học" chỉ hiện với Admin; mỗi trang bài học có nút quay lại danh sách.

## R2. CRUD dùng Modal kết hợp AJAX
- Mọi thao tác Thêm / Sửa / Xem chi tiết / Xoá (và các thao tác phụ: gán role, phân quyền, phê duyệt) mở trong
  Bootstrap 5 modal, submit bằng AJAX (`wwwroot/js/crud-modal.js`), lỗi validate hiển thị lại trong modal (HTTP 422),
  thành công thì đóng modal, hiện toast và tải lại vùng danh sách `[data-crud-list]` mà không reload trang.

## R3. Phân trang và tìm kiếm cho tất cả các trang danh sách
- Dùng `PagedList<T>.CreateAsync` (Skip/Take ở DB) + `_PagedListFooter`; giữ `q`, bộ lọc, `pageSize` khi chuyển trang;
  trang vượt quá số trang tự kẹp về trang cuối; tìm kiếm không phân biệt hoa thường (kể cả tiếng Việt có dấu).

## R4. CRUD Role
- Danh sách (tìm kiếm + phân trang + số người dùng mỗi role), Thêm, Sửa tên, Xem chi tiết, Xoá trong modal.
- Bảo vệ role hệ thống, kiểm tra trùng tên, xử lý role đang được gán cho người dùng.

## R5. Phân quyền động cho Role, áp dụng cho từng action của tất cả controller
- Quyền sinh tự động bằng reflection dạng `Controller.Action`; ma trận tích chọn theo module trong modal;
  thay đổi có hiệu lực ngay (xoá cache quyền); Admin luôn có toàn quyền; người không phải Admin không cấp được
  quyền mình không có.

## R6. Đăng nhập / đăng ký theo tư cách (theo sản phẩm mẫu)
- **Đăng nhập**: chọn tư cách "Sinh viên" hoặc "Giảng viên / Quản trị". Sai tư cách thì báo rõ, không đăng nhập.
- **Đăng ký Sinh viên**: Họ tên, **Mã sinh viên** (10 chữ số, bắt đầu bằng 2, duy nhất), Email, Mật khẩu, Xác nhận.
- **Đăng ký Giảng viên / Cán bộ**: thay Mã SV bằng **Mã cán bộ** (vd. `GV0123`, duy nhất); hiện thông báo
  "Tài khoản cần được Quản trị viên phê duyệt trước khi đăng nhập".
- Tài khoản giảng viên chưa duyệt không đăng nhập được (sau khi đã xác thực email).
- Trang **Người dùng** hiển thị tư cách, mã SV/mã cán bộ, trạng thái duyệt; có bộ lọc "Chờ duyệt" và thao tác
  **Phê duyệt** (AJAX, có quyền riêng `UserManagement.Approve`).
- Migration: tài khoản cũ được coi là đã duyệt; tài khoản Admin hiện có thuộc tư cách Giảng viên / Quản trị.

## R7. Hoàn thiện chung
- Giao diện đăng nhập/đăng ký dạng thẻ chọn tư cách như ảnh mẫu, responsive, hỗ trợ sáng/tối hiện có.
- Cập nhật `README.md` mục chức năng và tài khoản.

# Giai đoạn 3 — Kiểm chứng

1. `dotnet build` không lỗi; `dotnet test` toàn bộ xanh. Thêm test cho: filter `[AdminOnly]`
   (Admin vào được, role khác bị từ chối kể cả khi có quyền cũ), `Permissions.Discover` không còn khoá `Lesson*`/`Lec5*`,
   validate mã SV/mã cán bộ, đăng nhập sai tư cách, giảng viên chưa duyệt, thao tác Phê duyệt.
2. Chạy ứng dụng, đăng nhập bằng Admin và bằng tài khoản thường; kiểm tra thủ công từng trang theo bảng ở giai đoạn 1.
3. Áp dụng migration lên bản sao CSDL cũ để chắc dữ liệu cũ vẫn đăng nhập được.

# Định dạng báo cáo cuối

- Bảng đối chiếu trước/sau.
- Danh sách file đã thay đổi, mỗi file một dòng mô tả.
- Kết quả build/test (số test pass/fail) — báo trung thực, không che lỗi.
- Những việc chưa làm hoặc cần người dùng quyết định.
