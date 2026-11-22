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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<DepartmentModel>().HasData(
            new DepartmentModel { DepartmentCode = "CNTT", DepartmentName = "Công nghệ thông tin" },
            new DepartmentModel { DepartmentCode = "KT", DepartmentName = "Kinh tế" },
            new DepartmentModel { DepartmentCode = "XD", DepartmentName = "Xây dựng" },
            new DepartmentModel { DepartmentCode = "MO", DepartmentName = "Khoa học mỏ" },
            new DepartmentModel { DepartmentCode = "DC", DepartmentName = "Địa chất" });

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