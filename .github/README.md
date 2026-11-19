# Bộ Skill GitHub Copilot cho dự án quản lý ASP.NET Core MVC

## Cài đặt
Chép thư mục `.github/` vào **thư mục gốc repo** (merge nếu đã có):
```
.github/
├── copilot-instructions.md            # hướng dẫn chung (luôn được nạp)
├── instructions/
│   ├── controllers.instructions.md    # tự áp dụng cho Controllers/**/*.cs
│   └── views.instructions.md          # tự áp dụng cho Views/**/*.cshtml
└── skills/                            # Agent Skills (mỗi skill 1 thư mục + SKILL.md)
    ├── mvc-crud-ajax-modal/
    ├── pagination-search/
    ├── ef-core-migrations/
    ├── auto-code-generation/
    ├── dynamic-authorization/
    ├── excel-import/
    ├── identity-email/
    └── project-audit/
```

## Dùng
- Mở Copilot **Agent mode** trong VS Code; skill tự được nạp khi yêu cầu khớp phần `description`.
- Ví dụ: "Sinh CRUD modal Ajax cho entity Khoa", "Thêm phân trang và tìm kiếm cho trang Sinh viên", "Dùng skill project-audit kiểm tra dự án".
- Nếu phiên bản Copilot của bạn chưa hỗ trợ Agent Skills, vẫn dùng được: `copilot-instructions.md` và `instructions/*.md` hoạt động độc lập; nội dung từng `SKILL.md` có thể chuyển thành prompt file `.github/prompts/<tên>.prompt.md`.
