# Giao diện ArchitectUI

Khung trang dùng [ArchitectUI HTML Theme Dashboard Free](https://github.com/DashboardPack/architectui-html-theme-free)
v4.9.3 của DashboardPack (giấy phép MIT, bản sao tại `wwwroot/lib/architectui/LICENSE`).

`wwwroot/lib/architectui/architectui.min.css` là bản đã biên dịch từ `demomvc-architectui.scss` trong thư mục này.
Bản dựng gồm Bootstrap 5 của theme, khung trang (header, sidebar, main), các thành phần (nút, thẻ, bảng, modal,
phân trang…) và bỏ những phần dự án không dùng: font icon của theme (dự án dùng Bootstrap Icons), lịch, bản đồ,
toastr, perfect-scrollbar, animate-sass và trang demo. Không cần Node khi build hay deploy dự án.

Chỉ cần biên dịch lại khi đổi biến màu hoặc thêm thành phần của theme:

```powershell
git clone --depth 1 https://github.com/DashboardPack/architectui-html-theme-free.git <thư-mục-theme>
npx sass@1.108.2 --no-source-map --style=compressed --quiet-deps `
  --silence-deprecation=import,global-builtin,color-functions `
  --load-path=<thư-mục-theme>/src/assets `
  tools/architectui/demomvc-architectui.scss wwwroot/lib/architectui/architectui.min.css
```

Cấu trúc HTML của khung trang nằm trong `Views/Shared/_Layout.cshtml`; hành vi thu gọn/mở menu nằm trong
`wwwroot/js/site.js` (thay cho metisMenu và jQuery 4 của theme). Phần ghi đè màu, logo và chế độ tối nằm trong
`wwwroot/css/site.css`.
