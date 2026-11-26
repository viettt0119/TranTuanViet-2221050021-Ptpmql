# DemoMVC - Nội dung các buổi học

Dự án thực hành ASP.NET Core MVC, Entity Framework Core và SQLite. README này tập trung vào kiến thức từng buổi.

## Lec 1 - Tạo dự án MVC

- Tạo dự án ASP.NET Core MVC bằng .NET CLI.
- Tìm hiểu cấu trúc thư mục `Controllers`, `Models`, `Views`, `wwwroot`.
- Chạy ứng dụng ở môi trường Development.
- Ôn tập cấu trúc file `.csproj`, `Program.cs` và Razor view.

```powershell
dotnet new mvc -n DemoMVC
dotnet run --project .\DemoMVC.csproj
```

## Lec 2 - Routing, Controller và View

- Luồng xử lý request:

```text
Routing -> Controller -> Model -> Controller -> View
```

- Cấu trúc định tuyến conventional:

```text
/Controller/Action/Id
```

- Ví dụ:

```text
/Student/Details/11
```

Controller `Student` gọi action `Details` với tham số `id = 11`.

- Tách rõ trách nhiệm:
  - `Controller`: nhận request và phản hồi.
  - `Model`: đại diện dữ liệu và Entity Framework.
  - `View`: hiển thị dữ liệu bằng Razor.
- Tạo nhanh Controller bằng scaffolding:

```powershell
dotnet aspnet-codegenerator controller -name EmployeeController -m EmployeeModel -dc DemoMVC.Data.ApplicationDbContext --relativeFolderPath Controllers --useDefaultLayout --referenceScriptLibraries --databaseProvider sqlite
```

## Lec 3 - Cách truyền dữ liệu sang View

- `ViewBag`: dữ liệu động truyền từ Controller sang View.
- `ViewData`: dữ liệu truyền qua dictionary.
- `TempData`: dữ liệu lưu qua redirect.
- Strongly typed view: truyền entity hoặc `PagedList<T>` vào Razor.
- Tìm hiểu luồng model binding từ form POST vào action.

## Lec 4 - Ôn tập Routing và truyền dữ liệu

- Ôn tập conventional routing.
- Ôn tập `ViewBag`, `ViewData`, `TempData`.
- Nhận dữ liệu bằng strongly typed model.
- Tách form, danh sách, modal và partial view.
- Học cách dùng `Url.Action`, `asp-action`, `asp-route-id` trong Razor.

## Lec 5 - CRUD, Modal và Ajax

- CRUD cho `Student`, `Department`, `Employee`, `Product`.
- CRUD `Lec5` dùng dữ liệu trong bộ nhớ để minh hoạ.
- Giao diện Bootstrap modal.
- Gửi form bằng Fetch/Ajax.
- Trả lỗi validation HTTP 422 trong partial view.
- Sau khi lưu thành công, tự cập nhật danh sách và đóng modal.
- Anti-forgery token bắt buộc cho action POST.
- Không cho xóa Khoa đang có Sinh viên.

Các module chính:

- `/Student`
- `/Department`
- `/Employee`
- `/Product`
- `/Lec5`

## Lec 6 - Phân trang, tìm kiếm và CRUD nâng cao

### Tìm kiếm và phân trang

- Tìm kiếm phía máy chủ theo query `q`.
- Phân trang Entity Framework bằng `CountAsync`, `Skip` và `Take`.
- Hỗ trợ `pageSize`: 10, 20 hoặc 50.
- Trang vượt phạm vi được đưa về trang cuối.
- Tìm kiếm kết hợp cùng phân trang trên danh sách sản phẩm.

Ví dụ:

```text
/Product?q=mouse&page=2&pageSize=10
/Student?q=Nguyen&page=1&pageSize=20
```

### Khoá ngoại và sinh mã

- `Student.DepartmentCode` liên kết tới `Department.DepartmentCode`.
- Không cho xóa Khoa đang còn Sinh viên.
- Mã sinh viên được sinh theo nhóm năm nhập học, hệ đào tạo và ngành.
- Mã có dạng `YYHHNNSSSS`, ví dụ `2821050199`.
- Thay đổi bộ đếm và thêm Sinh viên cùng transaction.

### Kết nối SQLite và EF Core

- DbContext kế thừa `IdentityDbContext<ApplicationUser>`.
- Migration được lưu trong thư mục `Migrations`.
- Entity nghiệp vụ: Sinh viên, Khoa, Nhân viên, Sản phẩm.
- Startup tự migrate database khi ứng dụng chạy.

## Hoàn thiện dự án: bài học chỉ Admin, đăng nhập theo tư cách

Prompt kiểm tra và hoàn thiện toàn bộ dự án: [.github/prompts/hoan-thien-du-an.prompt.md](.github/prompts/hoan-thien-du-an.prompt.md).

### Xem lại bài học (chỉ Admin)
- Các bài thực hành Lec 1 – Lec 6 (`LessonController`, `Lesson4Controller`, `Lec5Controller`) gắn `[AdminOnly]`:
  chỉ role **Admin** vào được, kể cả khi gõ URL trực tiếp hay gọi Ajax. Các action này **không** nằm trên ma trận phân quyền
  nên không thể cấp cho role khác; quyền `Lesson.*`, `Lesson4.*`, `Lec5.*` cũ tự bị xoá khỏi CSDL khi khởi động.
- Trang **Xem lại bài học** (`/Lesson`): danh sách bài giảng + bài thực hành, tìm kiếm theo tên/chủ đề, lọc theo buổi, phân trang.
  Mục "Bài học" trên sidebar chỉ hiện với Admin.

### Đăng nhập / đăng ký theo tư cách
| Tư cách | Mã khi đăng ký | Sau khi đăng ký |
|---|---|---|
| Sinh viên | Mã sinh viên: 10 chữ số, bắt đầu bằng 2 (VD `2521050001`) | Xác thực email → đăng nhập, role `User` |
| Giảng viên / Cán bộ | Mã cán bộ: 2–4 chữ cái + 2–6 chữ số (VD `GV0123`) | Xác thực email **và** chờ Admin phê duyệt → role `GiangVien` |

- Khi đăng nhập phải chọn đúng tư cách; tài khoản Admin dùng tư cách **Giảng viên / Quản trị**.
- Mã SV / mã cán bộ là duy nhất.
- Admin phê duyệt tại **Người dùng** → lọc "Chờ duyệt" → **Duyệt** (quyền riêng `UserManagement.Approve`).
- Vai trò hệ thống `Admin`, `User`, `GiangVien` không đổi tên / xoá được; quyền của `User` và `GiangVien` vẫn chỉnh trên ma trận.
- Migration `AddAccountTypeAndApproval`: tài khoản cũ được coi là đã duyệt, tài khoản Admin cũ chuyển sang tư cách Giảng viên / Quản trị.
