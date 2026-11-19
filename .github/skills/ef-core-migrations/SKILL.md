---
name: ef-core-migrations
description: Tạo Entity, cấu hình khoá ngoại, quản lý phiên bản CSDL bằng EF Core Migrations và seed dữ liệu (vd các khoa của Trường Đại học Mỏ - Địa chất). Dùng khi thêm/sửa bảng hoặc dữ liệu khởi tạo.
---

# Skill: Entity, Migrations, Seed

## Quy trình thêm/sửa bảng
1. Sửa/tạo Entity trong `Models/` (có `[Required]`, `[StringLength]`, `[Display(Name="...")]` tiếng Việt).
2. Khai báo `DbSet<>` trong `AppDbContext` (kế thừa `IdentityDbContext<ApplicationUser, IdentityRole, string>`).
3. Cấu hình quan hệ/chỉ mục trong `OnModelCreating`.
4. Tạo migration: `dotnet ef migrations add <TenMoTaNgan> --project <DataProject> --startup-project <WebProject>`
5. **Đọc lại file migration** trước khi áp dụng (kiểm tra không xoá cột/bảng ngoài ý muốn).
6. Áp dụng: `dotnet ef database update`.
7. Commit cả `Migrations/*.cs` và `*ModelSnapshot.cs`.

## Quy tắc
- KHÔNG sửa hoặc xoá migration đã áp dụng/ đã push; muốn đổi thì tạo migration mới.
- Tên migration mô tả được: `AddKhoaAndSinhVienFK`, không dùng `Update1`.
- Kiểm tra lệch model: `dotnet ef migrations has-pending-model-changes` (EF Core 8+).
- Môi trường deploy: ưu tiên sinh script `dotnet ef migrations script --idempotent -o migrate.sql` hoặc chạy `Database.Migrate()` có kiểm soát; không tự `EnsureCreated()` cùng Migrations.

## Khoá ngoại mẫu (Sinh viên – Khoa qua mã Khoa)
```csharp
public class Khoa
{
    [Key, StringLength(10)] public string MaKhoa { get; set; } = null!;
    [Required, StringLength(200)] public string TenKhoa { get; set; } = null!;
    public ICollection<SinhVien> SinhViens { get; set; } = new List<SinhVien>();
}

public class SinhVien
{
    public int Id { get; set; }
    [Required, StringLength(10)] public string MaSV { get; set; } = null!;
    [Required, StringLength(150)] public string HoTen { get; set; } = null!;
    [Required] public string MaKhoa { get; set; } = null!;
    public Khoa? Khoa { get; set; }
}

// OnModelCreating
b.Entity<SinhVien>().HasIndex(s => s.MaSV).IsUnique();
b.Entity<SinhVien>().HasOne(s => s.Khoa).WithMany(k => k.SinhViens)
    .HasForeignKey(s => s.MaKhoa).OnDelete(DeleteBehavior.Restrict);
```

## Seed dữ liệu danh mục khoa (HasData)
- Dùng `modelBuilder.Entity<Khoa>().HasData(...)` với khoá cố định (MaKhoa) để migration ổn định.
- Danh sách các khoa/đơn vị đào tạo của Trường Đại học Mỏ - Địa chất: **hãy hỏi người dùng hoặc tra website chính thức của trường** để lấy tên khoa và mã chính xác, không tự bịa.
- Tài khoản/role mặc định KHÔNG seed qua HasData (cần hash mật khẩu) — xem skill `identity-email`.
