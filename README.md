### lec1 
- câu lệnh tạo dự án MVC : dotnet new mvc -n tên dự án / -o tên dự án
- deploy lên monterasp.net
- tên miền : http://lecture0119.runasp.net/
### lec2
1. Routing : định tuyến 
- Cấu trúc định tuyến bao gồm : domain/Controller_Name/Action/Id
- Ví dụ cơ bản về định tuyến : 1 url có tên như sau : domain/sanpham/chitiet/11 
- Lúc này định tuyển tới controller "sanpham" - controller chuyển tới action "chitiet" có trong controller 
- Nếu k có thì phải tạo action , kiểm tra các thành phần có đầy đủ hay không , sau đó để hàm chitiet xử lý thông tin mang id "11"
2. Controller : quản lý nhận yêu cầu từ Routing(định tuyến) , xử lý  và phản hồi thông tin
- View : Bộ mặt hiển thị giao diện và thông tin 
- Model : Quản lý và làm việc với dữ liệu 
    Luồng dữ liệu cơ bản : Routing - Controller - Model - Controller - View 
- Câu lệnh tạo nhanh Controller : ac_controller 
- Trong đó namespace Tên dự án.Controller,
- public class Tênfile + Controller :Controller
- Muốn hiển thị thông tin thì tạo thư mục trong Vỉew trùng tên với tên Controller vừa tạo, Các action hiển thị dưới dạng file .cshtml
### lec 4 
- Ôn tập về định tuyến , cơ bản cấu trúc 
- Học về viewbag, viewdata, gửi nhận dữ liệu qua model - controller - view
### lec 6 
#### Phân trang sản phẩm
- Danh sách sản phẩm hỗ trợ phân trang phía máy chủ tại `/Product`.
- Có thể truyền `page` và `pageSize` trên query string; `pageSize` được hỗ trợ là 10, 20 hoặc 50 (mặc định 10).
- Trang vượt phạm vi được đưa về trang cuối; trang không hợp lệ hoặc `pageSize` không hỗ trợ được chuẩn hóa về giá trị mặc định.
- Chạy kiểm thử phân trang bằng `dotnet test Tests\DemoMVC.Tests\DemoMVC.Tests.csproj`.
- CRUD Sản phẩm, Sinh viên, Khoa, Nhân viên và User trong Lec5 được thao tác trong Bootstrap modal qua AJAX; lỗi kiểm tra dữ liệu hiển thị ngay trong modal.
- Danh sách Sinh viên, Khoa, Sản phẩm và Nhân viên hỗ trợ tìm kiếm phía máy chủ theo từ khóa.
- Sinh viên vẫn được cấp mã tự động; không thể xóa Khoa đang có sinh viên.
- Dữ liệu User trong Lec5 vẫn lưu trong bộ nhớ của ứng dụng như cách triển khai ban đầu.
- Identity dùng Cookie authentication; đăng ký không bắt buộc xác thực email và sau khi tạo tài khoản người dùng có thể đăng nhập ngay. Đăng nhập bị khóa sau 5 lần sai trong 15 phút và các endpoint auth có rate limit.
- Tài khoản Admin và cấu hình SMTP (dùng cho email đặt lại mật khẩu, không dùng khi đăng ký) được cấp qua User Secrets hoặc biến môi trường; không điền secret vào `appsettings.json`.
- Chạy test: `dotnet test Tests\DemoMVC.Tests\DemoMVC.Tests.csproj`.
- Migration `AddAspNetCoreIdentity` được bổ sung vào source; cập nhật DB bằng `dotnet ef database update`. Ứng dụng cũng tự áp dụng migration lúc khởi động.
- Thiết lập secrets mẫu trong PowerShell (thay các giá trị placeholder tại máy chạy ứng dụng):
  ```powershell
  dotnet user-secrets set "AdminSeed:Email" "admin@example.com" --project .\DemoMVC.csproj
  dotnet user-secrets set "AdminSeed:FullName" "System Administrator" --project .\DemoMVC.csproj
  dotnet user-secrets set "AdminSeed:Password" "<strong-admin-password>" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:Host" "<smtp-host>" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:Port" "587" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:UseStartTls" "true" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:UserName" "<smtp-username>" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:Password" "<smtp-password>" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:SenderEmail" "<verified-sender@example.com>" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:SenderName" "DemoMVC" --project .\DemoMVC.csproj
  dotnet user-secrets set "Email:PublicBaseUrl" "https://<application-domain>" --project .\DemoMVC.csproj
  ```
- sinh code tự động 
- dotnet aspnet-codegenerator controller -name EmployeeController -m EmployeeModel -dc DemoMVC.Data.ApplicationDbContext --relativeFolderPath Controllers --useDefaultLayout --referenceScriptLibraries --databaseProvider sqlite
