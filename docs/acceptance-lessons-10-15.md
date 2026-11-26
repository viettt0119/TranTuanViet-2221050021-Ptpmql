# Nghiệm thu chức năng buổi 10–15

Tài liệu đối chiếu yêu cầu với mã nguồn DemoMVC và mô tả cách kiểm tra kết quả thực tế.
"Có mã nguồn/test" không đồng nghĩa đã nghiệm thu giao diện, SMTP hoặc hosting.
Phần ghi kết quả ở cuối tài liệu phải được cập nhật bằng lần chạy thực tế; không suy ra kết quả từ website mẫu.

## 1. Phạm vi và môi trường

- Nghiệp vụ lưu trong CSDL: Sinh viên, Khoa, Nhân viên, Sản phẩm.
- Quản trị hệ thống: Vai trò, Người dùng, Phân quyền, Nhập Excel.
- Bài học `Lesson`, `Lesson4`, `Lec5` dành riêng cho Admin. `Lec5` là dữ liệu thực hành trong bộ nhớ;
  không dùng làm dữ liệu nghiệp vụ và có thể mất khi khởi động lại.
- Chuẩn bị một tài khoản Admin, một sinh viên đã xác nhận email, một giảng viên đã duyệt,
  một giảng viên chờ duyệt và một vai trò thử nghiệm để kiểm tra quyền riêng lẻ.
- Dùng CSDL kiểm thử tách biệt. Ghi lại số bản ghi trước/sau từng thao tác import, xóa và restart.
- Tắt dữ liệu minh họa bằng `DemoDataSeed__Enabled=false` nếu cần chứng minh bản ghi đã xóa không xuất hiện lại.
  Mặc định Development bật, Production/Staging tắt. Thiết lập này không thay đổi migration Khoa hay seed Admin.

## 2. Yêu cầu → triển khai → nghiệm thu

| Yêu cầu | Thành phần triển khai | Kịch bản và kết quả cần đạt |
|---|---|---|
| 10. Khóa ngoại Sinh viên–Khoa | `StudentModel`, `DepartmentModel`, `ApplicationDbContext`, migrations | Thêm SV với Khoa hợp lệ; danh sách/chi tiết hiển thị tên Khoa. Gửi mã Khoa không tồn tại phải bị từ chối. Không xóa được Khoa đang có SV. |
| 10. Danh mục Khoa HUMG | `Data/UniversityDepartments.cs`, migrations danh mục Khoa | CSDL mới có danh mục seed; nâng cấp bản sao CSDL cũ không phá mã Khoa đang được SV tham chiếu. Không coi mã nội bộ dự án là mã đơn vị chính thức nếu chưa có đối chiếu. |
| 10. Sinh mã `YYHHNNSSSS` | `StudentCodeGenerator`, `StudentCodeCounter`, `StudentService` | Nhóm năm 2028/hệ 21/ngành 05 đã cấp 0199 thì bản ghi tiếp theo nhận `2821050200`; nhóm khác có bộ đếm riêng. Xóa SV không cấp lại số cũ. Hết 9999 báo lỗi. Import mã có sẵn nâng bộ đếm để lần thêm sau không trùng. |
| 11. CRUD modal/AJAX | `Views/*/_FormModal`, `_DetailsModal`, `_DeleteModal`, `wwwroot/js/crud-modal.js` | Thêm/sửa/xem/xóa không rời trang danh sách. Gửi form sai giữ modal và hiện lỗi; gửi hợp lệ đóng modal, thông báo, cập nhật danh sách. Không gửi hai lần khi nút đang xử lý. |
| 12. Tìm kiếm, phân trang | `PagedList`, các action `Index`, `_PagedListFooter` | Kiểm tra từng trang ở bảng mục 3; kết hợp từ khóa và bộ lọc; giữ bộ lọc, số dòng/trang sau phân trang và CRUD; trang vượt phạm vi được kẹp. Danh sách rỗng vẫn cho sửa/xóa bộ lọc. |
| 13. Identity và Admin ban đầu | `Program`, `IdentitySeeder`, `IdentitySeedOptionsValidator` | Khởi tạo Admin từ cấu hình hợp lệ; restart không tạo trùng hoặc đổi mật khẩu Admin hiện có. Thiếu cấu hình phải có thông báo vận hành, không giả định tồn tại mật khẩu mẫu. |
| 13. Xác nhận email | `AccountController.Register`, `ConfirmEmail`, `ResendConfirmation`, email services | Đăng ký mới chưa xác nhận không đăng nhập được; liên kết hợp lệ xác nhận đúng tài khoản; link lỗi/hết hạn có hướng dẫn gửi lại. Thư thực gửi tới đúng địa chỉ cần kiểm tra riêng với SMTP. |
| 13. Quên/đổi mật khẩu | `ForgotPassword`, `ResetPassword`, `ChangePassword`, `AuthTokenCodec` | Yêu cầu quên mật khẩu cho email có/không tồn tại cho phản hồi không tiết lộ tài khoản; link reset hợp lệ đổi được mật khẩu; link lỗi/hết hạn không đổi được; đăng nhập lại bằng mật khẩu mới. |
| 13. Loại hồ sơ và phê duyệt | `RegisterViewModel`, `MemberCodeUserValidator`, `UserManagement.Approve` | Mã SV/mã cán bộ đúng định dạng và duy nhất; một form đăng nhập email/mật khẩu cho mọi tài khoản; giảng viên cần cả xác nhận email và phê duyệt. Không duyệt tài khoản sinh viên thành giảng viên bằng cách sửa request. |
| 14. CRUD vai trò và gán role | `RoleController`, `UserManagementController` | Tạo/sửa/xem/xóa role qua modal; trùng tên có lỗi; role hệ thống không đổi tên/xóa; role đang có người dùng không xóa được. Gán/gỡ role hợp lệ và bảo vệ quyền quản trị. |
| 14. Quyền động từng action | `Permissions`, `PermissionCatalogService`, `PermissionAuthorizationFilter`, `PermissionTagHelper` | Ma trận có các action nghiệp vụ; GET/POST cùng tên dùng chung quyền; `ActionName` được tôn trọng. Bỏ một quyền thì nút ẩn và gọi URL/AJAX trực tiếp cũng bị từ chối. Người không phải Admin không tự cấp quyền cao hơn mình. |
| 15. Nhập Excel | `ExcelImportController`, `ExcelImportService`, `Views/ExcelImport` | Chọn đúng bảng/tải mẫu; xem trước dữ liệu và lỗi; trước xác nhận CSDL không đổi; chọn bổ sung hoặc ghi đè; kết quả ghi/giữ nguyên/lỗi phản ánh đúng dữ liệu. Chi tiết ở mục 4. |

## 3. Ma trận trang danh sách và thao tác

Các mục "có" dưới đây mô tả chức năng trong mã nguồn, chưa thay cho kết quả UI trên trình duyệt.

| Trang | Thêm/sửa/xem/xóa | Tìm kiếm/lọc | Phân trang | Quyền và lưu ý |
|---|---|---|---|---|
| `/Student` | Có modal; sinh mã tại server | Từ khóa, Khoa, năm nhập học | `PagedList`, truy vấn DB | Theo action; export là chức năng bổ sung ngoài yêu cầu import |
| `/Department` | Có modal; chặn xóa Khoa có SV | Mã/tên Khoa | `PagedList`, truy vấn DB | Theo action; chi tiết hiện SV thuộc Khoa |
| `/Employee` | Có modal | Mã/họ tên | `PagedList`, truy vấn DB | Theo action |
| `/Product` | Có modal | Tên sản phẩm | `PagedList`, truy vấn DB | Theo action; giá có giới hạn validation |
| `/Role` | Có modal | Tên role | `PagedList`, truy vấn DB | Chi tiết, gán quyền; bảo vệ role hệ thống và role đang được dùng |
| `/UserManagement` | Xem/gán role, xác nhận email/phê duyệt theo nghiệp vụ | Từ khóa, role, chờ duyệt | `PagedList`, truy vấn DB | Tạo tài khoản qua đăng ký; không coi là CRUD mật khẩu/bảng Identity trực tiếp |
| `/Lesson` | Danh mục nội dung, không phải CRUD | Từ khóa, buổi | Trong bộ nhớ vì catalog tĩnh | AdminOnly |
| `/Lec5` | Có modal, dữ liệu thực hành | Từ khóa, role | Trong bộ nhớ | AdminOnly; không phải bảng CSDL |
| `/ExcelImport/Preview`, `/ExcelImport/Result` | Báo cáo một lần nhập | Không phải trang CRUD danh sách | Không áp dụng ma trận phân trang nghiệp vụ | Cần kiểm tra giới hạn file, lỗi từng dòng và kết quả import |

Với mỗi danh sách, lặp lại các bước sau:

1. Tìm một chuỗi có kết quả, một chuỗi không có kết quả; thử chữ hoa/thường tiếng Việt.
2. Chọn bộ lọc (nếu có), chọn 10/20/50 dòng; chuyển trang rồi mở/đóng chi tiết.
3. Sửa một bản ghi đang có trong bộ lọc; danh sách cập nhật đúng dữ liệu, URL giữ bộ lọc.
4. Xóa bản ghi cuối ở trang cuối; trở về trang hợp lệ, bộ đếm và số trang không bị cũ.
5. Xóa bộ lọc để phục hồi danh sách; refresh URL và mở URL đã sao chép cho cùng trạng thái.
6. Với tài khoản chỉ xem, kiểm tra nút không xuất hiện; gọi action bị cấm trực tiếp phải bị từ chối.

## 4. Nghiệm thu Excel theo bảng

| Đối tượng nhập | Khóa đối chiếu | Tình huống cần kiểm tra |
|---|---|---|
| Khoa (`department`) | `DepartmentCode` | Mã mới, mã đã có, mã trùng trong file, tên rỗng/quá dài; ghi đè tên không làm mất SV đang tham chiếu |
| Sinh viên (`student`) | `StudentCode`; để trống để tự sinh | Khoa hợp lệ/không có, mã sai prefix năm/hệ/ngành, mã trùng; thông tin bắt buộc; email và các trường mở rộng nếu có cột; sinh mã sau import không trùng |
| Nhân viên (`employee`) | `EmpId` | Mã mới/trùng, họ tên thiếu/quá dài, ngày không hợp lệ; ghi đè đúng bản ghi |
| Sản phẩm (`product`) | `Id` | Id mới/trùng, tên sai độ dài, giá không phải số/ngoài giới hạn; preview không ghi dữ liệu |
| Phân quyền role (`rolepermission`) | `RoleName` + `Permission` | Role/action không tồn tại, giá trị quyền không hợp lệ; chỉ Admin được nhập; thay quyền cập nhật cache; không phá quy tắc role hệ thống |

Đây là danh sách các đích nhập đang được hỗ trợ. Các bảng nội bộ của Identity (hash mật khẩu, token, cookie,
claims), bộ đếm mã SV và catalog quyền không được mô tả là có chức năng nhập Excel độc lập.
Việc quản lý tài khoản và vai trò đi qua các luồng Identity/Role của ứng dụng.

Kịch bản chung cho mỗi đích nhập:

1. Chuẩn bị file có ít nhất một dòng mới, một dòng trùng CSDL, một dòng lỗi và một khóa lặp trong file.
2. Xem trước: kiểm tra khóa, dữ liệu nhận diện, trạng thái và lý do lỗi từng dòng; đối chiếu CSDL chưa thay đổi.
3. Chọn **bổ sung**: chỉ thêm dòng mới hợp lệ; bản ghi đã có được giữ nguyên và đếm là bỏ qua, không tính là ghi thành công.
4. Tải lại file và chọn **ghi đè**: kiểm tra đúng trường được cập nhật; trường không có trong file không bị mất ngoài ý muốn.
5. Nếu dữ liệu đã thay đổi sau preview, hệ thống phải yêu cầu xem trước lại hoặc báo xung đột rõ ràng;
   không âm thầm ghi đè quyết định dựa trên dữ liệu cũ.
6. Xác nhận lại cùng phiên, dùng phiên hết hạn hoặc phiên của tài khoản khác không được ghi thêm lần nữa.
7. Bỏ quyền cần thiết sau preview rồi xác nhận: quyền phải được kiểm tra lại theo bảng và chế độ nhập.
8. Kiểm tra file rỗng, sai định dạng, quá 5 MB; kết quả toàn lỗi, một phần thành công, chỉ bỏ qua và toàn thành công.

## 5. Kiểm chứng tự động và ghi nhận thực tế

Nhóm test liên quan trong `Tests/DemoMVC.Tests/`:

| Phần | Test/bằng chứng cần chạy |
|---|---|
| Sinh viên, sinh mã, import | `StudentServiceTests`, `ExcelImportServiceTests`, `ExcelImportControllerTests` (quyền theo bảng/chế độ, phiên xem trước, file hỏng) |
| Tìm kiếm/phân trang | `PagedListTests`, `SqliteUnicodeFunctionsInterceptorTests` |
| Đường dẫn form xoá | `DeleteFormActionTests`: không tạo URL từ tên phương thức đã đổi bằng `[ActionName]` |
| Identity, token, gửi thư | `AccountControllerTests`, `AccountTypeAndApprovalTests`, `IdentityTokenLifetimeTests`, `AuthTokenCodecTests`, `SmtpEmailSenderTests`, `MailKitEmailTransportTests` |
| Admin và quyền | `IdentitySeederTests`, `IdentitySeederPasswordResetTests`, `PermissionCatalogServiceTests`, `PermissionAuthorizationFilterTests` |
| Dữ liệu minh họa | `DemoDataSeederTests`: seed lặp không nhân bản; Production/Staging mặc định không tạo lại mẫu đã xóa; tắt explicit ở Development; bật explicit ngoài Development |

```powershell
dotnet build .\DemoMVC.csproj --no-restore
dotnet test .\Tests\DemoMVC.Tests\DemoMVC.Tests.csproj --no-restore
```

Kết quả đã được ghi nhận trước phiên khôi phục ngày 27/11/2026 (giữ lại để tham khảo lịch sử;
không coi các dòng dưới là kết quả chạy lại của phiên khôi phục):

| Kiểm tra | Kết quả |
|---|---|
| Build | `Release`: 0 lỗi, 0 cảnh báo. Restore project test có cảnh báo NU1903 cho `System.Security.Cryptography.Xml` |
| Test tự động | 173 đạt, 0 lỗi, 0 bỏ qua |
| CRUD modal/AJAX | Sinh viên, Khoa, Nhân viên, Sản phẩm, Lec5: thêm/sửa/xoá không tải lại trang; Vai trò: thêm, đổi tên, phân quyền |
| Tìm kiếm, phân trang | Sinh viên giữ từ khoá/năm/sắp xếp/số dòng sau sửa và xoá; xoá bản ghi cuối trang đưa về trang trước (Sinh viên, Sản phẩm) |
| Sinh mã | Thêm trên giao diện và nhập Excel sinh mã tăng dần; mã đã xoá không được cấp lại |
| Nhập Excel | Khoa (tài khoản chỉ có quyền Thêm: khoá ghi đè) và Sinh viên (xem trước thay đổi, ghi đè, nhập một phần) |
| Quyền và phê duyệt | Gán/gỡ vai trò, phê duyệt giảng viên; truy cập trực tiếp và AJAX khi thiếu quyền bị từ chối |
| UI desktop/mobile | Trình duyệt tích hợp (Chromium): 9 trang danh sách/nhập ở 1366, 375, 320px không cuộn ngang trang |
| Chưa chạy trên giao diện | Nhập Excel Nhân viên, Sản phẩm, Phân quyền; xoá Vai trò; đổi/quên/đặt lại mật khẩu (chỉ có test tự động) |
| Email thật | Chưa xác minh: SMTP bị tắt khi thử, xác thực bằng liên kết của môi trường Development |
| Migration | Chạy trên CSDL mới; chưa thử trên bản sao CSDL cũ (đợt này không thêm migration) |
| Hosting | Chưa deploy, chưa kiểm tra |

Tài liệu này không đánh dấu SMTP thật, UI toàn bộ vai trò hoặc hosting là đạt khi chưa có lần kiểm tra tương ứng.

## 6. Kiểm chứng lại khi tiếp tục từ HANDOFF — 27/11/2026

Trạng thái đầu phiên: HEAD `e872d8a`; `git status` chỉ có `.gitignore` thêm `HANDOFF.MD`, không có
thay đổi staged. HANDOFF là bản chép hội thoại, không phải bản tổng kết kỹ thuật. Các sửa AJAX Sinh viên
và giữ bộ lọc được nhắc trong hội thoại đã hiện diện ở HEAD. Build đầu phiên thành công; test đầu phiên
thực tế đạt 172/173, lỗi so sánh giá `1000.00` với `1000` phụ thuộc locale của máy.

Các lỗi đã sửa trong phiên:

- Giữ số dòng/trang khi tìm kiếm/xóa bộ lọc trên cả 8 danh sách; Khoa sắp xếp tên rồi mã để phân trang ổn định.
- Sản phẩm sửa/xóa và Nhân viên xóa trả 404 khi bản ghi vừa bị thao tác khác xóa, tránh 500.
- Excel xử lý mã SV nhập sẵn trước mã tự sinh; dòng đã tồn tại bị bỏ qua khi bổ sung vẫn đồng bộ bộ đếm.
  Phát hiện Id sản phẩm tương đương (`1`, `01`, `+1`) ngay ở preview.
- Giá Excel dùng giá trị số gốc, parser văn bản cố định dấu chấm thập phân; từ chối dấu phân cách hàng nghìn
  và dấu phẩy để tránh đọc `1234,50` thành `123450`. Preview, validation và ghi dữ liệu dùng cùng quy tắc.
- Gán/gỡ vai trò trong một transaction; lỗi ở một bước hoàn tác tất cả. Chặn lựa chọn role không tồn tại
  và bảo vệ Admin cuối cùng. Modal duyệt dùng cùng quyền `UserManagement.Approve` với POST.
- Test host ghim `System.Security.Cryptography.Xml` 10.0.13 theo
  [bản vá của .NET](https://github.com/dotnet/runtime/security/advisories/GHSA-7hqp-7c5m-4vq6);
  restore/build/test cuối không còn cảnh báo NU1903. Web app vẫn dùng shared framework hiện có.

| Kiểm tra của phiên này | Kết quả thực chạy |
|---|---|
| `dotnet build DemoMVC.csproj -c Release --no-restore` | Thành công, 0 lỗi, 0 cảnh báo |
| `dotnet test Tests/DemoMVC.Tests/DemoMVC.Tests.csproj -c Release --no-restore` | 191 đạt, 0 lỗi, 0 bỏ qua; thêm 18 ca hồi quy |
| `node --check` | Cả 4 file `wwwroot/js/*.js` hợp lệ |
| `git diff --check` | Không có lỗi khoảng trắng hoặc dấu conflict; Git có thông báo chuẩn hóa LF/CRLF |
| Khởi động HTTP trên CSDL tạm mới | `/healthz` trả 200/Healthy; migrations và dữ liệu mẫu chạy được |
| Đăng nhập HTTP | Cookie + anti-forgery thành công với Admin chỉ dùng cho kiểm thử |
| HTML thường + AJAX của 8 danh sách | Có `data-crud-list`, `pageSize=20` trong form tìm kiếm và link xóa lọc |
| CRUD Sinh viên qua HTTP/AJAX | Form sai trả 422/partial modal; thêm 11 SV, sửa/xem/xóa thành công; link phân trang giữ từ khóa/Khoa/năm/sort/dir/pageSize; xóa còn 10 bản ghi thì page 2 được kẹp về 1 |
| Endpoint modal các module khác | Khoa, Nhân viên, Sản phẩm, Vai trò, Lec5 trả modal hợp lệ; trang nhập Excel trả 200 |
| Trình duyệt của phiên này | Chưa chạy: công cụ báo `The browser access host is unavailable`; kiểm chứng HTTP không thay thế kiểm tra tương tác JavaScript/hiển thị |

Smoke HTTP dùng CSDL riêng trong `TestResults/`, không dùng `DemoMVC.db`. Script tạm
`TestResults/resume-http-smoke.py` và CSDL tạm bị gitignore, chỉ phục vụ phiên này. Đã dừng server local.
Build/test cuối chạy sau các sửa Excel bổ sung; smoke HTTP danh sách chạy trước lượt build cuối và
không kiểm thử import Excel thực tế (phần đó được kiểm tra bằng test SQLite/EPPlus).

Các giới hạn còn lại: SMTP thật, trình duyệt/mobile của bản sửa mới, nhập Excel trên UI cho mọi bảng,
migration trên bản sao CSDL cũ và hosting chưa được xác minh trong phiên này. Không thêm migration hoặc deploy.
Startup CSDL tạm vẫn có cảnh báo migration cũ dùng `PRAGMA foreign_keys` ngoài transaction; không sửa
lịch sử migration trong đợt này. Cần thử nâng cấp trên bản sao CSDL trước khi đưa lên hosting.
