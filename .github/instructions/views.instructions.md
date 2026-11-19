---
applyTo: "**/Views/**/*.cshtml"
---
- Dùng Bootstrap 5 (`data-bs-toggle`, `data-bs-target`, `btn-close`) — KHÔNG dùng cú pháp Bootstrap 3/4 (`data-toggle`, `.close`).
- Mọi form có `@Html.AntiForgeryToken()` hoặc dùng tag helper `<form asp-action=...>` (tự thêm token).
- Dùng tag helper (`asp-for`, `asp-action`, `asp-validation-for`) thay vì HTML helper cũ.
- Nội dung modal và bảng danh sách nằm trong Partial View (`_Form`, `_Table`, `_Delete`...).
- Bao `@section Scripts { <partial name="_ValidationScriptsPartial" /> }` ở trang có form.
- Không đặt `<style>`/`<script>` dài trong view; tách ra `wwwroot/css`, `wwwroot/js`.
- Văn bản hiển thị cho người dùng bằng tiếng Việt.
