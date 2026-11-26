using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace DemoMVC.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
    {
    }

    public DbSet<StudentModel> Students { get; set; }
    public DbSet<EmployeeModel> Employees { get; set; }
    public DbSet<ProductModel> Products { get; set; }
    public DbSet<DepartmentModel> Departments { get; set; }
    public DbSet<StudentCodeCounter> StudentCodeCounters { get; set; }
    public DbSet<ApplicationPermission> ApplicationPermissions { get; set; }
    public DbSet<ApplicationRolePermission> ApplicationRolePermissions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // Tìm kiếm không phân biệt hoa/thường cho tiếng Việt (SQLite lower() chỉ xử lý ASCII).
        // Interceptor tự bỏ qua kết nối không phải SQLite.
        optionsBuilder.AddInterceptors(SqliteUnicodeFunctionsInterceptor.Instance);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Các khoa của Trường Đại học Mỏ - Địa chất. Giữ nguyên mã cũ (CNTT, KT, XD, MO, DC) vì sinh viên đang tham chiếu.
        builder.Entity<DepartmentModel>().HasData(UniversityDepartments.All
            .Select(item => new DepartmentModel { DepartmentCode = item.Code, DepartmentName = item.Name }));

        builder.Entity<ApplicationUser>(entity =>
        {
            // Mã SV / mã cán bộ duy nhất; SQLite cho phép nhiều NULL (tài khoản cũ, Admin chưa có mã).
            entity.HasIndex(x => x.MemberCode).IsUnique();
            // Mặc định ở CSDL = true: tài khoản có sẵn trước khi thêm cột được coi là đã duyệt.
            // HasSentinel(true): EF chỉ bỏ cột khỏi INSERT khi giá trị là true, nên false (chờ duyệt) luôn được ghi.
            // Thiếu dòng này, false trùng sentinel mặc định và bị CSDL thay bằng true → giảng viên tự được duyệt.
            entity.Property(x => x.IsApproved).HasDefaultValue(true).HasSentinel(true);
        });

        builder.Entity<ApplicationPermission>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(150);
            entity.Property(x => x.Controller).HasMaxLength(100);
            entity.Property(x => x.Action).HasMaxLength(100);
            entity.Property(x => x.Area).HasMaxLength(50);
            entity.Property(x => x.Module).HasMaxLength(50);
            entity.Property(x => x.ModuleDisplayName).HasMaxLength(50);
            entity.Property(x => x.ActionDisplayName).HasMaxLength(50);
            entity.Property(x => x.Group).HasMaxLength(50);
        });

        builder.Entity<ApplicationRolePermission>(entity =>
        {
            entity.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}