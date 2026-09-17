using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;

namespace DemoMVC.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
    {
    }

    public DbSet<StudentModel> Students { get; set; }
}