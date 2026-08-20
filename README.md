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