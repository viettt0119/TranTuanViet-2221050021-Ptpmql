# BÁO CÁO KIỂM TRA NGHIỆM THU

Ngày kiểm tra: 2026-11-19
Môi trường kiểm tra: Windows, thư mục `D:\tmp\DemoMVC`

## 1. Tổng quan

| STT | Nhóm yêu cầu | Trạng thái | Ước tính hoàn thành |
|---|---|---|---|
| 1 | Môi trường lập trình | ⚠️ Làm một phần | 55% |
| 2 | CI/CD | ⚠️ Làm một phần | 60% |
| 3 | Kiến trúc MVC | ✅ Đạt | 90% |
| 4 | Định tuyến | ⚠️ Làm một phần | 55% |
| 5 | Truyền dữ liệu M-V-C | ✅ Đạt | 100% |
| 6 | Quản lý trạng thái | ✅ Đạt | 90% |
| 7 | Layout | ✅ Đạt | 100% |
| 8 | Kết nối CSDL và CRUD | ✅ Đạt | 90% |
| 9 | Ôn tập CRUD | ⚠️ Làm một phần | 85% |
| 10 | Khoá ngoại và sinh mã tự động | ✅ Đạt | 95% |
| 11 | CRUD bằng Ajax/Modal | ✅ Đạt | 95% |
| 12 | Phân trang và tìm kiếm | ⚠️ Làm một phần | 60% |
| 13 | Xác thực và uỷ quyền (phần 1) | ⚠️ Làm một phần | 80% |
| 14 | Xác thực và uỷ quyền (phần 2) | ✅ Đạt | 95% |
| 15 | Nhập dữ liệu từ Excel | ❌ Chưa làm | 5% |

**Tỷ lệ hoàn thành trung bình ước tính:** khoảng **78%**.

## 2. Cấu trúc dự án

- Controllers: `D:\tmp\DemoMVC\Controllers`
  - `AccountController.cs`
  - `DepartmentController.cs`
  - `EmployeeController.cs`
  - `HomeController.cs`
  - `Lec5Controller.cs`
  - `Lesson4Controller.cs`
  - `LessonController.cs`
  - `Lec5Controller.cs`
  - `ProductController.cs`
  - `RoleController.cs`
  - `StudentController.cs`
  - `UserManagementController.cs`
- Models: `D:\tmp\DemoMVC\Models`
  - Entity chính: `StudentModel`, `DepartmentModel`, `EmployeeModel`, `ProductModel`
  - Identity: `ApplicationUser`, `ApplicationPermission`, `ApplicationRolePermission`
  - ViewModel/Identity account: `LoginViewModel`, `RegisterViewModel`, `ChangePasswordViewModel`, `ResetPasswordViewModel`, `ForgotPasswordViewModel`
- Views: `D:\tmp\DemoMVC\Views`, phân theo tên controller.
- Data/EF: `D:\tmp\DemoMVC\Data\ApplicationDbcontext.cs`
- Services: `D:\tmp\DemoMVC\Services`
- Migrations: `D:\tmp\DemoMVC\Migrations`
- Tests: `D:\tmp\DemoMVC\Tests\DemoMVC.Tests`
- Static assets: `D:\tmp\DemoMVC\wwwroot`
- CI/CD: `D:\tmp\DemoMVC\.github\workflows\deploy.yml`

## 3. Chi tiết từng mục

### 1. Môi trường lập trình

**Trạng thái:** ⚠️ Làm một phần - khoảng 55%.

**Bằng chứng:**
- `DemoMVC.csproj:2-7` đặt `Microsoft.NET.Sdk.Web`, `TargetFramework=net10.0`.
- `dotnet --info`: SDK 10.0.302, ASP.NET Core runtime 10.0.10.
- Không tìm thấy `global.json` trong thư mục dự án.
- Không tìm thấy thư mục `.vscode/` hoặc file `.vscode/extensions.json`.
- README có hướng dẫn chạy/test và EF migration tại `README.md:29-48`, nhưng không có hướng dẫn cài VS Code, extension, SDK rõ ràng qua `dotnet`.

**Thiếu gì:**
- `global.json` để pin SDK.
- Cấu hình khuyến nghị `.vscode/extensions.json` và `.vscode/settings.json`.
- Hướng dẫn cài đặt C# Dev Kit, cài SDK và khởi chạy.

**Đề xuất:** Thêm `global.json` cùng hướng dẫn VS Code trong README.

### 2. CI/CD

**Trạng thái:** ⚠️ Làm một phần - khoảng 60%.

**Bằng chứng:**
- Workflow push `main` có setup .NET, publish và deploy FTP: `.github/workflows/deploy.yml:1-37`.
- Workflow dùng secrets `FTP_USERNAME`, `FTP_PASSWORD`, `FTP_SERVER`: `.github/workflows/deploy.yml:13-21`, `:29-37`.
- README ghi tên miền cũ `lecture0119.runasp.net` ở `README.md:1-4`, không xác nhận domain hiện tại hay xác nhận deploy thành công.
- Có chứng từ cấu hình publish chạy `dotnet publish`: `.github/workflows/deploy.yml:13-21`.
- Không có Dockerfile, Azure Pipeline, GitLab CI.

**Thiếu gì:**
- Không có bước restore/build/test riêng trước deploy.
- Không có health check sau deploy.
- Không có mục giữ/backup database SQLite trước deploy.
- Không có migration deployment rõ ràng.
- Không tìm thấy cấu hình tên miền mới trong nguồn ngoài workflow.

**Đề xuất:** Thêm bước `dotnet restore`, `dotnet build`, `dotnet test`; cấu hình secrets hosting vào GitHub; chạy health check; backup và migrate DB nếu có.

### 3. Kiến trúc MVC

**Trạng thái:** ✅ Đạt - khoảng 90%.

**Bằng chứng:**
- Tách `Models/`, `Views/`, `Controllers/`, `Data/`, `Services/` rõ ràng.
- Entity và ViewModel nằm trong `Models/`.
- EF Core nằm trong `Data/ApplicationDbcontext.cs:7-45`.
- Razor views phân theo controller trong `Views/`.
- Controller chỉ phụ trách tiếp nhận route/request và phản hồi.

**Nhận xét logic nghiệp vụ:**
- `StudentController.CreateStudentWithCodeAsync` chứa giao dịch, SQL UPSERT và sinh mã: `Controllers/StudentController.cs:197-227`. Logic này quá nhiều trong Controller.
- Các CRUD khác truy cập DbContext trực tiếp trong Controller: `DepartmentController.cs:38-52`, `EmployeeController.cs:57-67`, `ProductController.cs:51-60`.
- Có thể chấp nhận ở bài MVC nhỏ, nhưng logic nghiệp vụ lớn không nên nằm trực tiếp trong controller.

**Thiếu gì:** Tách Student code generator/transaction sang Service/Repository để Controller mỏng hơn.

### 4. Định tuyến

**Trạng thái:** ⚠️ Làm một phần - khoảng 55%.

**Bằng chứng conventional routing:**
- `Program.cs:106-108` cấu hình static/routing/auth.
- Route mặc định: `Program.cs:112-114`: `{controller=Student}/{action=Index}/{id?}`.
- Home redirect sau đăng nhập: `Controllers/AccountController.cs:122-124`.

**Xử lý lỗi:**
- `UseExceptionHandler("/Home/Error")`: `Program.cs:95-103`.
- Không tìm thấy `UseStatusCodePagesWithReExecute`.
- Có view lỗi chung `Views/Shared/Error.cshtml:1-23`.
- Không có custom view/action 404 riêng bằng status code.

**Attribute routing:**
- Không tìm thấy `[Route(...)]`, `[HttpGet("...")]` hoặc `[HttpPost("...")]` trong Controllers. Các controller dùng conventional routing và thuộc tính HTTP verb theo cách MVC truyền thống.

**Thiếu gì:** Trang 404 tùy chỉnh và re-execute status code.

### 5. Truyền dữ liệu M-V-C

**Trạng thái:** ✅ Đạt - 100%.

**Ví dụ Controller không dùng Model:**
- `Lec5Controller.Index`: `ViewBag.Title`, `ViewBag.DemoViewBag`, `ViewData["DemoViewData"]`: `Controllers/Lec5Controller.cs:38-43`.
- View minh hoạ: `Views/Lec5/Index.cshtml:3-6`.
- `StudentController.Index` truyền `ViewData["SearchQuery"]`: `Controllers/StudentController.cs:35`.

**Ví dụ qua strongly typed Model:**
- Student: `return View(students)`: `Controllers/StudentController.cs:34-35`; view `@model IEnumerable<DemoMVC.Models.StudentModel>`: `Views/Student/Index.cshtml:1`.
- Product: `PagedList<ProductModel>`: `Controllers/ProductController.cs:28-33`; `Views/Product/Index.cshtml:1`.
- Department: `Views/Department/Index.cshtml:1` với `IEnumerable<DepartmentModel>`.
- Employee: `Views/Employee/Index.cshtml:1` với `IEnumerable<EmployeeModel>`.

**Controller/view minh hoạ:** `Student`, `Department`, `Product`, `Employee`, `Role`, `UserManagement`.

### 6. Quản lý trạng thái

**Trạng thái:** ✅ Đạt - khoảng 90%.

**ViewBag:**
- `Lec5Controller`: `ViewBag.Title`, `ViewBag.DemoViewBag`, `ViewBag.RoleList`: `Controllers/Lec5Controller.cs:16-17`, `38-42`, `62`.
- `StudentController.LoadDepartmentsAsync`: `ViewBag.DepartmentCode`, `ViewBag.HasDepartments`: `Controllers/StudentController.cs:183-185`.
- Hiển thị `ViewBag.RoleList` trong `Views/Lec5/_FormModal.cshtml:24`.

**ViewData:**
- `SearchQuery`, `ModalTitle`, `FormActionUrl`, `IsCreate`: `Controllers/StudentController.cs:35,52-54`; `Controllers/ProductController.cs:33,46-47`.
- Hiển thị trong layout `Views/Shared/_Layout.cshtml:9`.
- Account: `RegisteredEmail`, `Succeeded`, `StatusMessage`: `Controllers/AccountController.cs:61,72-98,300-305`.

**TempData:**
- Role delete: `TempData["ErrorMessage"]`: `Controllers/RoleController.cs:158-172`.
- Hiển thị sau redirect: `Views/Role/Index.cshtml:8`.
- `Keep()`/`Peek()` không được sử dụng. Không bắt buộc cho luồng hiện tại.

**Thiếu gì:** Không có `Keep()/Peek()` trong luồng thật; lưu ý chỉ là phần demo trong view, không phải logic code.

### 7. Layout

**Trạng thái:** ✅ Đạt - 100%.

**Bằng chứng:**
- Bootstrap 5 local: `Views/Shared/_Layout.cshtml:10-12`.
- CSS riêng `wwwroot/css/my-css.css` và Razor scoped CSS `Views/Shared/_Layout.cshtml.css`.
- Không tìm thấy CDN Bootstrap hoặc Bootstrap 3/4 trong Layout.
- Bootstrap bundle local: `Views/Shared/_Layout.cshtml:96-99`.
- Không thấy Bootstrap 3/4 lẫn lộn.

### 8. Kết nối CSDL và CRUD

**Trạng thái:** ✅ Đạt - khoảng 90%.

**Bằng chứng:**
- Chuỗi kết nối SQLite: `appsettings.json:2-4`.
- DbContext đăng ký: `Program.cs:9-12`.
- `ApplicationDbContext` kế thừa `IdentityDbContext<ApplicationUser>`: `Data/ApplicationDbcontext.cs:7-19`.
- DbSet: Student, Employee, Product, Department, StudentCodeCounter, Permission, RolePermission: `Data/ApplicationDbcontext.cs:13-19`.
- Migration toàn bộ: các file trong `Migrations/`.
- `dotnet ef migrations has-pending-model-changes --no-build`: "No changes have been made to the model since the last migration".
- Migration list cho thấy hai migration Identity/permission đang `Pending`: `20261112065020_AddAspNetCoreIdentity`, `20261112073642_Gd2Permissions`.
- Migration được tự apply khi app chạy: `Program.cs:119-121`.

**Thiếu gì:** Cần database thực tế đã chạy migration để xác nhận hai migration pending được apply; migration list theo Db hiện tại cho biết Pending.

### 9. Ôn tập CRUD

**Trạng thái:** ⚠️ Làm một phần - khoảng 85%.

**Validation/anti-forgery:** Các entity có DataAnnotations tại `Models/StudentModel.cs`, `DepartmentModel.cs`, `EmployeeModel.cs`, `ProductModel.cs`; POST CRUD đều có `[ValidateAntiForgeryToken]`, ví dụ `Controllers/DepartmentController.cs:38-39`, `ProductController.cs:51-52`, `EmployeeController.cs:57-58`, `StudentController.cs:58-59`.

**Thiếu:** `Lec5Model` dùng role data trong memory, không phải CSDL; không phải entity chính bền vững.

**Entity × CRUD:**

| Entity | Index | Details | Create | Edit | Delete | Ghi chú |
|---|---:|---:|---:|---:|---:|---|
| StudentModel | ✅ | ✅ | ✅ | ✅ | ✅ | Modal/Ajax |
| DepartmentModel | ✅ | ✅ | ✅ | ✅ | ✅ | Không cho xóa khi còn sinh viên |
| EmployeeModel | ✅ | ✅ | ✅ | ✅ | ✅ | Modal/Ajax |
| ProductModel | ✅ | ✅ | ✅ | ✅ | ✅ | Modal/Ajax + phân trang |
| Lec5Model (memory) | ✅ | ✅ | ✅ | ✅ | ✅ | Không lưu CSDL |
| IdentityRole | ✅ | ❌ | ✅ | ✅ | ✅ | CRUD role, không cần Details |
| ApplicationUser | ✅ | ❌ | ❌ | ⚠️ gán/bỏ role | ❌ | Không phải CRUD tự tạo bởi app |

### 10. CRUD nâng cao - khoá ngoại và sinh mã

**Trạng thái:** ✅ Đạt - khoảng 95%.

**Khoá ngoại:**
- Student.DepartmentCode trỏ đến Department: `Models/StudentModel.cs:30-35`.
- `[ForeignKey]` và `[DeleteBehavior(Restrict)]`: `Models/StudentModel.cs:33-34`.
- Khoa/Students có navigation: `Models/DepartmentModel.cs:11-14`.
- Không cho xóa khoa còn sinh viên: `Controllers/DepartmentController.cs:117-121`.

**Seed khoa đại học:**
- Không tìm thấy `HasData` hoặc seed các khoa thật của trường trong `Program.cs`, `ApplicationDbContext`, migration hoặc controller.
- Điều này là một phần thiếu; tên mặc định của trường chỉ thấy `University="Đại học Mỏ - Địa chất"` tại `Models/StudentModel.cs:24`.

**Form hiển thị Khoa:**
- Dropdown: `Views/Student/_FormModal.cshtml:50`.
- Load danh sách khoa: `Controllers/StudentController.cs:181-185`.
- Hiển thị tên khoa trong danh sách: `Views/Student/Index.cshtml:38-52`.

**Sinh mã:**
- Lấy năm/hệ/ngành từ form: `Controllers/StudentController.cs:58-67`.
- Upsert counter theo khoá `(AdmissionYear, TrainingSystemCode, MajorCode)`: `Models/StudentCodeCounter.cs:8-19`; SQL: `Controllers/StudentController.cs:197-209`.
- Dạng mã: 2 số năm + 2 hệ + 2 ngành + 4 thứ tự: `Controllers/StudentController.cs:219-221`.
- Ví dụ `2821050199` đúng với năm 28, hệ 21, ngành 05, thứ tự 0199.
- Bộ đếm riêng theo tổ hợp: `Models/StudentCodeCounter.cs:11-14`, tên comment "Mỗi nhóm..." `:8`.
- Tiếp theo tăng từ `LastNumber + 1`, ví dụ 0199 thành 0200.
- SQLite UPSERT + transaction: `Controllers/StudentController.cs:197-227`.
- Vượt 9999 trả lỗi: `Controllers/StudentController.cs:211-212`.
- Tính đồng thời: UPSERT SQLite trong transaction làm bộ đếm nguyên tử trong mức SQLite hỗ trợ; thêm StudentCode unique index: `Models/StudentModel.cs:7`.
- Khi thêm lưu sinh viên trùng mã do ngoại lệ khác bộ đếm vẫn có thể DB bắt unique; controller bắt `DbUpdateException`.

**Thiếu:** Không tìm thấy seed khoa cụ thể của trường.

### 11. Ajax/Modal

**Trạng thái:** ✅ Đạt - khoảng 95%.

**Cơ chế:**
- Host modal chung: `Views/Shared/_CrudModalHost.cshtml:1-2`.
- Tải partial bằng fetch: `wwwroot/js/crud-modal.js:37-57`.
- Submit POST/Ajax: `crud-modal.js:60-119`.
- Success reload danh sách sau khi modal đóng: `crud-modal.js:89-102`.
- Partial form/delete.
- Validation server trả 422 partial: ví dụ `Controllers/DepartmentController.cs:145-163`, `StudentController.cs:162-176`.

**Entity × Ajax/Modal:**

| Entity | Create | Edit | Details | Delete |
|---|---|---|---|---|
| StudentModel | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| DepartmentModel | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| EmployeeModel | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| ProductModel | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| Lec5Model | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |

**Thiếu:** Không tải lại danh sách bằng DOM patch/Ajax; sau thành công mã JS reload toàn trang (`crud-modal.js:97-102`). Yêu cầu "không reload trang" chưa đạt hoàn toàn.

### 12. Phân trang và tìm kiếm

**Trạng thái:** ⚠️ Làm một phần - khoảng 60%.

**Bảng đối chiếu:**

| Trang danh sách | Tìm kiếm | Phân trang DB |
|---|---:|---:|
| `/Product/Index` | ✅ `q` | ✅ `Skip/Take` |
| `/Student/Index` | ✅ `q` | ❌ |
| `/Department/Index` | ✅ `q` | ❌ |
| `/Employee/Index` | ✅ `q` | ❌ |
| `/Role/Index` | ❌ | ❌ |
| `/UserManagement/Index` | ❌ | ❌ |
| `/Lec5/Index` | ❌ | ❌ |

**Bằng chứng:**
- Product lọc trước `PagedList.CreateAsync`: `Controllers/ProductController.cs:20-31`.
- Student filter: `Controllers/StudentController.cs:19-34`.
- Department filter: `Controllers/DepartmentController.cs:13-26`.
- Employee filter: `Controllers/EmployeeController.cs:22-35`.
- `Skip/Take` và `CountAsync`: `Models/PagedList.cs:26-42`.
- Product giữ `q` trong form/pagination: `Views/Product/Index.cshtml:13-27`, `62-104`.

**Thiếu:** Chỉ Product phân trang. Student/Department/Employee/Lec5/Role/User chưa phân trang.

### 13. Xác thực và uỷ quyền (phần 1)

**Trạng thái:** ⚠️ Làm một phần - khoảng 80%.

**Identity:**
- `ApplicationUser : IdentityUser`, `ApplicationDbContext : IdentityDbContext<ApplicationUser>`: `Models/ApplicationUser.cs:1-11`, `Data/ApplicationDbcontext.cs:7-19`.
- AddIdentity, role, lockout, cookie: `Program.cs:17-51`.
- Password policy 12 ký tự, số, chữ thường/hoa, ký tự đặc biệt: `Program.cs:20-26`.
- Lockout 5 lần/15 phút: `Program.cs:27-29`.
- `RequireConfirmedEmail=false`: `Program.cs:33`. Tài khoản mới không bắt buộc xác thực theo yêu cầu mới.

**Seed tài khoản/role:**
- Seed role `Admin`, `User`: `Services/IdentitySeeder.cs:10-20`.
- Seed admin từ options: `Services/IdentitySeeder.cs:22-82`.
- Admin password lấy config/secret, không trực tiếp tạo chuỗi trong method.

**Nhưng thông tin nhạy cảm lộ trong repo:**
- `appsettings.json:5-17` chứa admin password `AdminPassword!123`, email, SMTP password `ChangeMe!123`, public URL.
- Đây là placeholder/secret mặc định nhưng vẫn bị commit; không an toàn cho production.
- README hướng dẫn User Secrets: `README.md:39-55`.
- CI deploy không thấy inject appsettings admin/SMTP, chỉ FTP. Secrets runtime chưa được xác nhận.

**Email:**
- `IEmailSender`, `SmtpEmailSender`, `IEmailTransport`, `MailKitEmailTransport`: `Services/SmtpEmailSender.cs:1-43`, `Services/MailKitEmailTransport.cs:1-58`.
- Forgot/reset password: `Controllers/AccountController.cs:180-260`.
- Confirm email flow còn tồn tại: `Controllers/AccountController.cs:66-98`, nhưng không bắt buộc đăng nhập do `RequireConfirmedEmail=false`.
- MailKit retry: `Services/SmtpEmailSender.cs:15-42`.

**Thiếu:** Xoá hoặc thay password/SMTP placeholder và xác nhận secret ở hosting.

### 14. Xác thực và uỷ quyền (phần 2)

**Trạng thái:** ✅ Đạt - khoảng 95%.

**CRUD Role:**
- Index/Create/Edit/Delete: `Controllers/RoleController.cs:13-39`, `47-67`, `150-173`.
- Có chống xóa Admin và role đang có user: `Controllers/RoleController.cs:156-169`.

**Gán role:**
- Gán/bỏ role người dùng: `Controllers/UserManagementController.cs:52-90`.
- Chống bỏ Admin khỏi quản trị viên duy nhất: `Controllers/UserManagementController.cs:75-82`.

**Cơ chế lưu quyền:**
- `ApplicationPermission`, `ApplicationRolePermission`: `Models/ApplicationPermission.cs:1-18`, `Models/ApplicationRolePermission.cs:1-19`.
- Quan hệ và unique `(RoleId, PermissionId)`: `Data/ApplicationDbcontext.cs:34-44`.

**Cơ chế kiểm tra:**
- Global action filter: `Services/PermissionAuthorizationFilter.cs:10-69`.
- Handler/policy động: `Services/PermissionAuthorizationHandler.cs:1-29`, `Services/PermissionAuthorizationPolicyProvider.cs:5-25`.
- Catalog reflection và sync DB: `Services/PermissionCatalogService.cs:8-56`, discovery `:117-160`.
- Admin bypass: `Services/PermissionAuthorizationFilter.cs:43-48`.
- Default User permissions: `Services/PermissionCatalogService.cs:91-115`.
- Global filter đăng ký: `Program.cs:58-61`.

**Ma trận role/action:**
- UI checkboxes: `Views/Role/Permissions.cshtml:1-30`.
- POST lưu IsGranted: `Controllers/RoleController.cs:108-141`.

**Toàn bộ action và bảo vệ:**

| Controller | Actions | Bảo vệ |
|---|---|---|
| Account | Register, ConfirmEmail, Login, Logout, AccessDenied, ResendConfirmation, ForgotPassword, ResetPassword, ChangePassword | Global filter; AllowAnonymous cho register/login/confirm/reset/forgot; `[Authorize]` Logout/ChangePassword |
| Home | Index, Privacy, Error | Global; Privacy/Error AllowAnonymous |
| Student | Index, Details, Create, Edit, DeleteConfirmed | Global permission filter |
| Department | Index, Details, Create, Edit, DeleteConfirmed | Global permission filter |
| Employee | Index, Details, Create, Edit, DeleteConfirmed | Global permission filter |
| Product | Index, Details, Create, Edit, DeleteConfirmed | Global permission filter |
| Role | Index, Create, Edit, Permissions, Delete | Global permission filter + `[Authorize]` class |
| UserManagement | Index, AssignRole, RemoveRole | Global permission filter + `[Authorize]` class |
| Lec5 | Index, Details, Create, Edit, DeleteConfirmed | Global permission filter |
| Lesson4 | Index, Post action | Global permission filter; cụ thể tên Post cần xem file |
| Lesson | Index hoặc các action hiển thị lesson | Global permission filter; không có [AllowAnonymous] |

**Hạn chế:** Không thấy `[Route]`/`[Authorize(Policy="Permission...")]` theo từng action. Protection là global theo controller/action và permission catalog; chính xác nhưng phân quyền phụ thuộc startup sync.

### 15. Nhập dữ liệu Excel

**Trạng thái:** ❌ Chưa làm - khoảng 5%.

**Bằng chứng:**
- EPPlus có trong package reference: `DemoMVC.csproj:12`.
- Không tìm thấy `ExcelPackage`, `ExcelWorksheet`, `IFormFile`, action Import hoặc file mẫu `.xlsx/.csv`.
- Không có chức năng ghi đè/bổ sung, xử lý từng dòng/transaction import.

**Thiếu:**
- Import cho Student, Department, Employee, Product và mọi entity CSDL khác.
- File mẫu.
- Chọn ghi đè/bổ sung.
- Validation định dạng, dữ liệu trùng, khoá ngoại, dòng lỗi.
- EPPlus license setting theo giấy phép EPPlus 8 và môi trường triển khai.

## 4. Các bảng đối chiếu

### CRUD entity

| Entity | Model | Index | Details | Create | Edit | Delete | Validation |
|---|---|---|---|---|---|---|---|
| Student | Có | Có | Có | Có | Có | Có | DataAnnotations + ModelState |
| Department | Có | Có | Có | Có | Có | Có | DataAnnotations + ModelState |
| Employee | Có | Có | Có | Có | Có | Có | DataAnnotations + ModelState |
| Product | Có | Có | Có | Có | Có | Có | DataAnnotations + ModelState |
| Lec5Model | Có (không CSDL) | Có | Có | Có | Có | Có | ModelState tùy chọn |
| IdentityRole | Có | Có | Không có view chi tiết | Có | Có | Có | Identity role validation |
| ApplicationUser | Có | Có | Không có CRUD view | Không tạo trực tiếp | Gán/bỏ role | Không xoá | Identity |

### Ajax/Modal

| Entity | Create | Edit | Details | Delete |
|---|---|---|---|---|
| Student | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| Department | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| Employee | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| Product | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |
| Lec5 | Modal/Ajax | Modal/Ajax | Modal/Ajax | Modal/Ajax |

### Phân trang và tìm kiếm

Xem bảng mục 12.

### Import Excel

| Entity | Import | File mẫu | Ghi đè/bổ sung | Validation từng dòng |
|---|---|---|---|---|
| Student | ❌ | ❌ | ❌ | ❌ |
| Department | ❌ | ❌ | ❌ | ❌ |
| Employee | ❌ | ❌ | ❌ | ❌ |
| Product | ❌ | ❌ | ❌ | ❌ |
| Lec5/Identity | Không áp dụng | Không áp dụng | Không áp dụng | Không áp dụng |

### Phân quyền

| Controller | Action Level | Admin | Role User | Cơ chế |
|---|---|---|---|---|
| Student | CRUD | Toàn quyền | CRUD mặc định | Reflection + RolePermission |
| Department | CRUD | Toàn quyền | CRUD mặc định | Reflection + RolePermission |
| Employee | CRUD | Toàn quyền | CRUD mặc định | Reflection + RolePermission |
| Product | CRUD | Toàn quyền | CRUD mặc định | Reflection + RolePermission |
| Role | CRUD/Matrix | Toàn quyền | Không mặc định | Reflection + RolePermission |
| UserManagement | Gán/bỏ role | Toàn quyền | Không mặc định | Reflection + RolePermission |
| Account | Auth | Theo role | Tự đổi mật khẩu/đăng xuất | Identity + global filter |
| Lesson/Lesson4/Lec5 | Bài học/CRUD mẫu | Theo quyền | Lec5 không mặc định | Global filter |

## 5. Vấn đề chất lượng/bảo mật phát hiện

1. **Cao: Secret hard-code trong `appsettings.json`**
   - `appsettings.json:5-17` lưu mật khẩu admin và SMTP placeholder.
   - Cần chuyển toàn bộ secret sang User Secrets/Environment Variables/hosting secret store.
2. **Cao: Không có `.gitignore`**
   - Không tìm thấy `.gitignore`.
   - Build output, SQLite WAL/SHM, `obj/`, `bin/` có thể bị commit. Kết quả git hiện có build artifacts chưa clean.
3. **Cao: Build đã thành công nhưng package vulnerability warnings**
   - `dotnet build` thành công, 6 warnings gồm NU1901/NU1902/NU1903 và cảnh báo nullable ở `PermissionAuthorizationPolicyProvider.cs:24`.
   - Cần upgrade NuGet vulnerable packages và sửa nullability.
4. **Trung bình: Migration Identity/Permission đang Pending**
   - Migration list ghi hai migration Pending: `20261112065020`, `20261112073642`.
   - Startup gọi `Database.MigrateAsync`, `Program.cs:119-121`, nên app sẽ tự apply.
5. **Trung bình: No 404/status re-execute**
   - Chỉ có `UseExceptionHandler`, không có custom 404 path.
6. **Trung bình: Tài nguyên có thể reload toàn trang sau AJAX**
   - `crud-modal.js:89-102` reload sau lưu.
   - Yêu cầu không reload chưa đạt tuyệt đối.
7. **Trung bình: Logic nghiệp vụ trong Controller**
   - Student tạo mã và transaction trực tiếp trong Controller: `Controllers/StudentController.cs:197-227`.
   - Khuyến nghị chuyển sang service.
8. **Trung bình: Dữ liệu phụ Lec5 chỉ nằm memory**
   - `Controllers/Lec5Controller.cs:10-11` dùng `ConcurrentDictionary`.
   - Không có persistence nên CRUD Lec5 mất sau restart.
9. **Thấp: Không có data seed cho khoa thực tế**
   - Không tìm thấy `HasData` hoặc seed.
10. **Thấp: Không có sample `.xlsx`**
11. **Thấp: Hosting/domain ngoài repo chưa xác nhận**

## 6. Kết quả chạy thử

- `dotnet --info`: thành công; SDK 10.0.302, .NET runtime 10.0.10.
- `dotnet build DemoMVC.csproj --no-restore`: **thành công**, 0 lỗi, 6 cảnh báo.
- `dotnet ef migrations list --no-build`: thành công; hai migration Identity/Permission hiện Pending trên database kết nối.
- `dotnet ef migrations has-pending-model-changes --no-build`: kết luận không có pending model changes so snapshot.
- `dotnet test Tests/DemoMVC.Tests/DemoMVC.Tests.csproj --no-restore`: **35/35 passed**.
- NuGet warnings: NU1901, NU1902, NU1903; cần xem `dotnet list package --vulnerable`.
- Nullable warning: `PermissionAuthorizationPolicyProvider.cs:24`.

## 7. Danh sách việc cần làm theo ưu tiên

### Cao

1. Xoá/replace `AdminSeed:Password` và `Email:Password` trong `appsettings.json`; dùng User Secrets, biến môi trường hoặc secret store hosting.
2. Thêm `.gitignore` cho `bin/`, `obj/`, `appsettings.Development.json`, SQLite `*.db-shm`, `*.db-wal`, credential files.
3. Khắc phục package vulnerable NU1901/NU1902/NU1903 và chạy lại `dotnet list package --vulnerable`.
4. Xác nhận migration Identity/Permission đã apply trên database production và backup trước deploy.
5. Kiểm tra deploy FTP thực tế, domain hiện tại và secrets workflow. Chưa thể xác nhận bằng source.

### Trung bình

6. Bổ sung trang 404 với `UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}")` hoặc handler tương đương.
7. Thay reload toàn trang trong CRUD Ajax bằng cập nhật DOM hoặc reload chỉ vùng bảng.
8. Tách Student code generator và transaction sang service/repository.
9. Áp dụng phân trang cho Student, Department, Employee, Role, User và Lec5 nếu Lec5 tiếp tục là bài mẫu.
10. Bổ sung seed khoa thực của Trường Đại học Mỏ - Địa chất bằng `HasData` hoặc migration/seed startup.
11. Khắc phục cảnh báo nullability tại `PermissionAuthorizationPolicyProvider.cs:24`.
12. Thêm `.vscode/extensions.json`, README hướng dẫn VS Code và pin SDK bằng `global.json`.

### Thấp

13. Bổ sung import Excel, file mẫu và quy trình ghi đè/bổ sung.
14. Bổ sung health check và CI test/build trước FTP deploy.
15. Bổ sung `Keep()`/`Peek()` nếu luồng UI thực sự cần đọc TempData nhiều lần.
