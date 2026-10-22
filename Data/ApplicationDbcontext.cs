using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;

namespace DemoMVC.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
    {
    }

    public DbSet<StudentModel> Students { get; set; }
    public DbSet<EmployeeModel> Employees { get; set; } 
    public DbSet<ProductModel> Products { get; set; }
    public DbSet<DepartmentModel> Departments { get; set; }
    public DbSet<StudentCodeCounter> StudentCodeCounters { get; set; }
}