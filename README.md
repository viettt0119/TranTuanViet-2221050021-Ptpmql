# DemoMVC - Nội dung các buổi học

Dự án thực hành ASP.NET Core MVC, Entity Framework Core và SQLite. README này tập trung vào kiến thức từng buổi.

## Đối chiếu yêu cầu buổi 10–15

Ma trận chức năng, các bước nghiệm thu và phạm vi kiểm tra được lưu tại
[docs/acceptance-lessons-10-15.md](docs/acceptance-lessons-10-15.md).
Tài liệu phân biệt chức năng có trong mã nguồn với kết quả kiểm thử và các luồng cần kiểm tra trên trình duyệt.

| Buổi | Chức năng trong dự án |
|---|---|
| 10 | Quan hệ Sinh viên–Khoa, danh mục Khoa HUMG, sinh mã `YYHHNNSSSS` bằng bộ đếm theo nhóm năm/hệ/ngành |
| 11 | CRUD qua Bootstrap modal và AJAX; validation trong modal, thông báo và cập nhật danh sách |
| 12 | Tìm kiếm, bộ lọc và phân trang tại các trang danh sách; giữ trạng thái khi chuyển trang |
| 13 | ASP.NET Core Identity, tài khoản Admin từ cấu hình, xác nhận email, gửi lại liên kết, quên/đổi mật khẩu |
| 14 | CRUD vai trò, gán/gỡ vai trò, ma trận quyền theo `Controller.Action`, kiểm tra quyền ở server |
| 15 | Nhập Excel cho Sinh viên, Khoa, Nhân viên, Sản phẩm và phân quyền vai trò; xem trước rồi chọn bổ sung/ghi đè |

## Giao diện

Khung trang quản trị dùng theme ArchitectUI HTML Free (DashboardPack, giấy phép MIT), đã biên dịch sẵn tại
`wwwroot/lib/architectui/architectui.min.css`. Cách biên dịch lại và phạm vi theme được ghi tại
[tools/architectui/README.md](tools/architectui/README.md). Build và deploy dự án không cần Node.

## Dữ liệu khởi tạo và cấu hình chạy

- Danh mục Khoa được quản lý qua migration trong `Migrations/` và `Data/UniversityDepartments.cs`.
- `AdminSeed:Email`, `AdminSeed:Password`, `AdminSeed:FullName` cấu hình tài khoản quản trị ban đầu.
  Dùng User Secrets hoặc biến môi trường `AdminSeed__Email`, `AdminSeed__Password`, `AdminSeed__FullName`;
  không ghi mật khẩu vào mã nguồn hay tệp trong `wwwroot`.
- Cấu hình email theo `Models/EmailSettings.cs`; `Email:PublicBaseUrl` là địa chỉ công khai dùng trong liên kết email.
  Gửi email thực tế cần cấu hình SMTP hợp lệ; test tự động không chứng minh thư đã tới hộp thư người nhận.
- Dữ liệu minh họa gồm 20 sinh viên và 20 nhân viên, nhận diện bằng email `student.sample.*@example.invalid`
  và mã `DEMO-EMP-*`. Đây là dữ liệu hồ sơ, không tạo tài khoản đăng nhập cho các sinh viên/nhân viên này.
- `DemoDataSeed:Enabled` điều khiển dữ liệu minh họa. Nếu không cấu hình, chỉ môi trường `Development` bật seed;
  `Production` và `Staging` tắt. Có thể dùng biến môi trường `DemoDataSeed__Enabled=false` để tắt cả ở local.
  Khi bật, startup bổ sung các mẫu còn thiếu; khi tắt, startup không tạo lại mẫu đã xóa và không tự xóa mẫu đã có.
  Chỉ đặt `DemoDataSeed__Enabled=true` trên môi trường demo khi chủ động muốn dữ liệu minh họa.

Chạy và kiểm tra từ thư mục dự án:

```powershell
dotnet run --project .\DemoMVC.csproj
dotnet build .\DemoMVC.csproj --no-restore
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj --no-restore
```

Hai lệnh kiểm tra dùng `--no-restore` cần các gói đã được restore. Chạy `dotnet restore` khi chuẩn bị máy mới.
Nghiệm thu thay đổi dữ liệu trên CSDL kiểm thử riêng hoặc bản sao CSDL, không dùng trực tiếp dữ liệu hosting.

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

## Tài khoản, đăng nhập và quyền truy cập

Prompt kiểm tra và hoàn thiện toàn bộ dự án: [.github/prompts/hoan-thien-du-an.prompt.md](.github/prompts/hoan-thien-du-an.prompt.md).

### Xem lại bài học (chỉ Admin)
- Các bài thực hành Lec 1 – Lec 6 (`LessonController`, `Lesson4Controller`, `Lec5Controller`) gắn `[AdminOnly]`:
  chỉ role **Admin** vào được, kể cả khi gõ URL trực tiếp hay gọi Ajax. Các action này **không** nằm trên ma trận phân quyền
  nên không thể cấp cho role khác; quyền `Lesson.*`, `Lesson4.*`, `Lec5.*` cũ tự bị xoá khỏi CSDL khi khởi động.
- Trang **Xem lại bài học** (`/Lesson`): danh sách bài giảng + bài thực hành, tìm kiếm theo tên/chủ đề, lọc theo buổi, phân trang.
  Mục "Bài học" trên sidebar chỉ hiện với Admin.

### Đăng ký theo loại hồ sơ, đăng nhập chung
| Tư cách | Mã khi đăng ký | Sau khi đăng ký |
|---|---|---|
| Sinh viên | Mã sinh viên: 10 chữ số, bắt đầu bằng 2 (VD `2521050001`) | Xác thực email → đăng nhập, role `User` |
| Giảng viên / Cán bộ | Mã cán bộ: 2–4 chữ cái + 2–6 chữ số (VD `GV0123`) | Xác thực email **và** chờ Admin phê duyệt → role `GiangVien` |

- Mọi tài khoản dùng một trang **Đăng nhập tài khoản**, chỉ nhập email và mật khẩu.
  Loại hồ sơ được chọn khi đăng ký; quyền được xác định từ các vai trò đã cấp trong CSDL.
- Đăng nhập thông thường chuyển đến **Trang chủ** (`/Home/Dashboard`) cho cả Admin, sinh viên và cán bộ.
  Liên kết nội bộ cần đăng nhập vẫn có thể quay lại qua `returnUrl`; liên kết ngoài bị từ chối.
- Khách mở website thấy `/Home/Index`. Người đã đăng nhập mở `/` hoặc trang đăng nhập sẽ vào Trang chủ.
  Đăng xuất trở về Index. Truy cập chức năng nội bộ khi chưa đăng nhập sẽ yêu cầu đăng nhập.
- Mã SV / mã cán bộ là duy nhất.
- Mục **Tài khoản & phân quyền** chứa hai tab **Người dùng** và **Vai trò & quyền**. Mỗi tab chỉ hiện
  khi có quyền xem tương ứng; các nút quản lý kiểm tra quyền thao tác riêng. Tài khoản sinh viên/cán bộ
  theo quyền mặc định không thấy mục này và bị chặn khi gọi URL/AJAX quản trị trực tiếp.
- Admin phê duyệt tại **Tài khoản & phân quyền → Người dùng** → lọc "Chờ duyệt" → **Duyệt**
  (quyền riêng `UserManagement.Approve`). Email xác thực và phê duyệt vẫn là điều kiện đăng nhập.
- Vai trò hệ thống `Admin`, `User`, `GiangVien` không đổi tên / xoá được; quyền của `User` và `GiangVien` vẫn chỉnh trên ma trận.
- Migration `AddAccountTypeAndApproval`: tài khoản cũ được coi là đã duyệt, hồ sơ Admin cũ thuộc nhóm Cán bộ.
