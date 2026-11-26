# DemoMVC - Phần mềm quản lý ASP.NET Core MVC

Dự án thực hành xây dựng bằng ASP.NET Core MVC, Entity Framework Core, SQLite và ASP.NET Core Identity. README mô tả quy trình chạy dự án và nội dung của từng bài học.

## Yêu cầu môi trường

- .NET SDK 10.0 trở lên
- Visual Studio Code hoặc Visual Studio
- EF Core CLI:

```powershell
dotnet tool install --global dotnet-ef
```

- SQLite được sử dụng mặc định thông qua `DemoMVC.db`
- Khi gửi email cần tài khoản SMTP hợp lệ

Khuyến nghị cài các extension:

- C# Dev Kit
- C#

Phiên bản SDK được pin trong `global.json`.

## Tạo và chạy dự án

Tạo dự án MVC ban đầu:

```powershell
dotnet new mvc -n DemoMVC -o DemoMVC
cd DemoMVC
```

Khôi phục package và chạy ứng dụng:

```powershell
dotnet restore
dotnet build
dotnet run
```

Mở URL hiển thị trong terminal. Theo `Properties/launchSettings.json`:

- `dotnet run`: `http://localhost:5123`
- `dotnet run --launch-profile https`: `https://localhost:7044`

## Cấu hình database

Chuỗi kết nối SQLite nằm trong `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=DemoMVC.db; Cache=Shared"
  }
}
```

Tạo hoặc cập nhật migration:

```powershell
dotnet ef migrations add TenMigration
dotnet ef database update
```

Kiểm tra migration pending:

```powershell
dotnet ef migrations list
dotnet ef migrations has-pending-model-changes
```

Ứng dụng tự gọi migration khi khởi động. Nên sao lưu `DemoMVC.db` trước khi cập nhật production.

## Cấu hình tài khoản Admin

Không lưu mật khẩu thật trong `appsettings.json` hay `appsettings.Development.json` (cả hai file đều được git theo dõi). Thiết lập bằng User Secrets:

```powershell
dotnet user-secrets set "AdminSeed:Email" "admin@example.com" --project .\DemoMVC.csproj
dotnet user-secrets set "AdminSeed:FullName" "System Administrator" --project .\DemoMVC.csproj
dotnet user-secrets set "AdminSeed:Password" "<mật-khau-admin-mạnh>" --project .\DemoMVC.csproj
```

Hoặc thiết lập bằng biến môi trường:

```powershell
$env:AdminSeed__Email="admin@example.com"
$env:AdminSeed__FullName="System Administrator"
$env:AdminSeed__Password="<mật-khau-admin-mạnh>"
```

Mật khẩu phải đáp ứng chính sách Identity:

- Ít nhất 12 ký tự
- Có chữ hoa
- Có chữ thường
- Có chữ số
- Có ký tự đặc biệt
- Có ít nhất 4 ký tự không trùng nhau

Cách hoạt động khi ứng dụng khởi động:

- Thiếu `AdminSeed:Email`, `AdminSeed:Password` hoặc `AdminSeed:FullName` (mặc định `System Administrator`) thì chỉ ghi cảnh báo và bỏ qua.
- Chạy nhiều lần không tạo trùng. Admin được tạo với email đã xác thực.
- Nếu email đã thuộc một tài khoản chưa phải Admin, tài khoản đó được đặt lại mật khẩu theo cấu hình và gán role `Admin`.
- Mật khẩu của admin đã tồn tại được giữ nguyên, trừ khi bật `AdminSeed:ResetPassword=true`.

## Cấu hình gửi email (Gmail SMTP)

SMTP được dùng cho:

- Gửi liên kết xác thực email
- Gửi liên kết quên/đặt lại mật khẩu
- Gửi thông báo sau khi đổi mật khẩu

### Tạo App Password

1. Bật xác minh 2 bước cho tài khoản Google dùng để gửi thư (Tài khoản Google > Bảo mật).
2. Mở <https://myaccount.google.com/apppasswords>, đặt tên ứng dụng (ví dụ `DemoMVC`) rồi tạo.
3. Sao chép mã 16 ký tự (Google chỉ hiện một lần). Đây không phải mật khẩu Gmail.

### Cấu hình local

Chạy script và nhập theo hướng dẫn (App Password không bị lưu vào lịch sử lệnh):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\set-gmail-user-secrets.ps1
```

Hoặc đặt từng giá trị:

```powershell
dotnet user-secrets set "Email:UserName" "<dia-chi-gmail>" --project .\DemoMVC.csproj
dotnet user-secrets set "Email:Password" "<app-password-16-ky-tu>" --project .\DemoMVC.csproj
```

Giá trị mặc định trong `appsettings.json`: `Email:Host=smtp.gmail.com`, `Email:Port=587`, `Email:UseStartTls=true`, `Email:SenderName=DemoMVC`. Để trống `Email:SenderEmail` thì dùng `Email:UserName`. Ở local nên để trống `Email:PublicBaseUrl` để liên kết trong thư theo đúng địa chỉ đang chạy.

## Biến môi trường trên hosting

Trên MonsterASP.NET, khai báo tại **Scripting > Environment variables** rồi khởi động lại ứng dụng.

| Biến | Cần khai báo | Ghi chú |
|---|---|---|
| `AdminSeed__Email` | Có | Email tài khoản quản trị |
| `AdminSeed__Password` | Có | Theo chính sách mật khẩu ở trên |
| `AdminSeed__FullName` | Không | Mặc định `System Administrator` |
| `AdminSeed__ResetPassword` | Khi cần | Đặt `true` để đặt lại mật khẩu admin, xong thì xoá |
| `Email__UserName` | Có | Địa chỉ Gmail gửi thư |
| `Email__Password` | Có | App Password 16 ký tự |
| `Email__PublicBaseUrl` | Có | Địa chỉ công khai của website. Để trống thì liên kết lấy theo header Host (có thể bị giả mạo) và ứng dụng ghi cảnh báo |
| `Email__SenderEmail`, `Email__SenderName` | Không | Địa chỉ và tên người gửi |
| `Email__Host`, `Email__Port`, `Email__UseStartTls` | Không | Chỉ khi không dùng Gmail |
| `DataProtection__KeysPath` | Không | Mặc định dùng thư mục `private` cạnh `wwwroot` nếu có |

## Chạy unit test

Chạy toàn bộ test:

```powershell
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj
```

Chạy test phân trang:

```powershell
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj --filter "FullyQualifiedName~PagedListTests"
```

Chạy test phân quyền:

```powershell
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj --filter "FullyQualifiedName~PermissionCatalogServiceTests"
```

Chạy test nhập Excel:

```powershell
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj --filter "FullyQualifiedName~ExcelImportServiceTests"
```

## Nội dung từng bài thực hành

### Lec 1 - Tạo dự án MVC

- Tạo dự án ASP.NET Core MVC bằng .NET CLI.
- Tìm hiểu cấu trúc thư mục `Controllers`, `Models`, `Views`, `wwwroot`.
- Chạy ứng dụng ở môi trường Development.
- Deploy ứng dụng lên hosting hỗ trợ ASP.NET Core.
- Ôn tập cấu trúc file `.csproj`, `Program.cs` và Razor view.

```powershell
dotnet new mvc -n DemoMVC
dotnet run --project .\DemoMVC.csproj
```

### Lec 2 - Routing, Controller và View

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

### Lec 3 - Cách truyền dữ liệu sang View

- `ViewBag`: dữ liệu động truyền từ Controller sang View.
- `ViewData`: dữ liệu truyền qua dictionary.
- `TempData`: dữ liệu lưu qua redirect.
- Strongly typed view: truyền entity hoặc `PagedList<T>` vào Razor.
- Tìm hiểu luồng model binding từ form POST vào action.

### Lec 4 - Ôn tập Routing và truyền dữ liệu

- Ôn tập conventional routing.
- Ôn tập `ViewBag`, `ViewData`, `TempData`.
- Nhận dữ liệu bằng strongly typed model.
- Tách form, danh sách, modal và partial view.
- Học cách dùng `Url.Action`, `asp-action`, `asp-route-id` trong Razor.

### Lec 5 - CRUD, Modal và Ajax

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

### Lec 6 - Phân trang, tìm kiếm và CRUD nâng cao

#### Tìm kiếm và phân trang

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

#### Khoá ngoại và sinh mã

- `Student.DepartmentCode` liên kết tới `Department.DepartmentCode`.
- Không cho xóa Khoa đang còn Sinh viên.
- Mã sinh viên được sinh theo nhóm năm nhập học, hệ đào tạo và ngành.
- Mã có dạng `YYHHNNSSSS`, ví dụ `2821050199`.
- Thay đổi bộ đếm và thêm Sinh viên cùng transaction.

#### Kết nối SQLite và EF Core

- DbContext kế thừa `IdentityDbContext<ApplicationUser>`.
- Migration được lưu trong thư mục `Migrations`.
- Entity nghiệp vụ: Sinh viên, Khoa, Nhân viên, Sản phẩm.
- Startup tự migrate database khi ứng dụng chạy.

## Xác thực và phân quyền Identity

### Đăng nhập, đăng ký và bảo vệ tài khoản

- ASP.NET Core Identity với Cookie authentication.
- User mới nhận role `User`.
- Tài khoản bị khóa sau 5 lần đăng nhập sai trong 15 phút.
- Endpoint xác thực dùng rate limiting.
- `RequireConfirmedEmail=true`: người tự đăng ký phải mở liên kết trong email (hiệu lực 24 giờ) và bấm xác nhận rồi mới đăng nhập được.
- Chưa nhận được thư thì dùng **Gửi lại email xác thực**; quản trị viên cũng có thể xác thực thủ công ở trang Người dùng.
- Liên kết đặt lại mật khẩu hiệu lực 1 giờ và chỉ dùng được một lần. Đặt lại thành công thì email được coi là đã xác thực và khóa tạm do nhập sai được gỡ (khóa dài hạn giữ nguyên).
- Trang quên mật khẩu hiện cùng một thông báo dù email có tồn tại hay không.
- Đổi mật khẩu xong, hệ thống gửi email thông báo.

### Khởi tạo tài khoản mặc định

Startup seed:

- Role `Admin`
- Role `User`
- Tài khoản Admin từ `AdminSeed`
- Quyền mặc định cho role `User`

### CRUD Role

Trang quản lý role:

- `/Role/Index`
- `/Role/Create`
- `/Role/Edit`
- `/Role/Delete`
- `/Role/Permissions`

Không cho xóa hoặc đổi tên role hệ thống `Admin`.

### Gán Role cho người dùng

Trang `/UserManagement/Index` cho phép:

- Xem role của từng người dùng.
- Gán role.
- Gỡ role.
- Không cho gỡ role `Admin` khỏi quản trị viên duy nhất.

### Phân quyền động theo action

Hệ thống phát hiện permission bằng reflection từ:

- Tên Controller
- Tên Action
- Attribute `[AllowAnonymous]`

Permission có dạng:

```text
Student.Index
Student.Create
Student.Edit
Department.Delete
Role.Permissions
```

`PermissionAuthorizationFilter` kiểm tra quyền trước khi action chạy:

- Admin bỏ qua kiểm tra và có toàn quyền.
- User kiểm tra role và permission được ghi trong database.
- AJAX trả HTTP 403 dạng JSON.
- Request thường chuyển sang trang Access Denied.

## Nhập dữ liệu bằng Excel

Trang nhập Excel:

```text
/ExcelImport
```

Chức năng hỗ trợ:

- Khoa
- Nhân viên
- Sản phẩm
- Sinh viên

Không nhập các bảng Identity.

### Quy trình nhập

1. Chọn loại dữ liệu.
2. Tải file mẫu `.xlsx` tương ứng.
3. Điền dữ liệu theo tiêu đề cột.
4. Tải file lên để xem trước.
5. Kiểm tra tổng dòng, dòng hợp lệ, dòng mới và dòng trùng khoá.
6. Chọn một trong hai chế độ:
   - **Ghi bổ sung**: bỏ qua dữ liệu trùng khoá.
   - **Ghi đè**: cập nhật dữ liệu đã tồn tại.
7. Xác nhận và thực thi trong transaction.

File tải lên:

- Chỉ chấp nhận `.xlsx`.
- Tối đa 5 MB.
- Phải có hàng tiêu đề và dữ liệu hợp lệ.
- Sinh viên cần mã Khoa tồn tại.

Mẫu cột:

```text
Khoa:
DepartmentCode | DepartmentName

Nhân viên:
EmpId | FullName | HireDate

Sản phẩm:
Id | Name | Price

Sinh viên:
StudentCode | FullName | Address | University | DepartmentCode |
AdmissionYear | TrainingSystemCode | MajorCode
```

Nếu `StudentCode` để trống, hệ thống sinh mã mới theo bộ đếm tự động.

## CI/CD

Workflow `.github/workflows/deploy.yml` thực hiện:

1. Checkout source.
2. Cài .NET SDK.
3. Restore package.
4. Build project.
5. Chạy unit test.
6. Publish bản Release.
7. Tắt server tạm thời bằng `app_offline.htm`.
8. Deploy FTP.
9. Bật lại server.
10. Thực hiện health check nếu có secret `APP_HEALTH_URL`.

GitHub Actions cần các secrets:

```text
FTP_USERNAME
FTP_PASSWORD
FTP_SERVER
APP_HEALTH_URL
```

Các secret runtime của ứng dụng phải được cấu hình trên hosting bằng biến môi trường hoặc secret store.

## Cấu trúc thư mục

```text
Controllers/       Controller và action MVC
Models/            Entity, ViewModel, cấu hình
Views/             Razor view và partial view
Data/              ApplicationDbContext
Services/          Identity, permission, email, Excel và sinh mã
Migrations/        EF Core migrations
Tests/             Unit test xUnit
wwwroot/           CSS, JavaScript, thư viện Bootstrap
.github/workflows  GitHub Actions CI/CD
```

## Câu lệnh hữu ích

```powershell
dotnet restore
dotnet build
dotnet run --project .\DemoMVC.csproj
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj
dotnet ef migrations add TenMigration
dotnet ef database update
dotnet ef migrations list
dotnet ef migrations has-pending-model-changes
dotnet list .\DemoMVC.csproj package --vulnerable --include-transitive
```

## Địa chỉ deploy

Tên miền hosting trước đó:

```text
http://lecture0119.runasp.net/
```

Cần xác nhận tên miền và cấu hình secret thật trên hosting trước production.
