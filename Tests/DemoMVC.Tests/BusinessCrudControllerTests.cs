using DemoMVC.Controllers;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public class BusinessCrudControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Product_mutation_returns_not_found_when_another_request_deleted_the_record(bool delete)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var product = new ProductModel { Name = "Sản phẩm thử", Price = 1000 };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // The controller has already loaded the entity when another request deletes it.
        await using (var otherRequest = CreateContext(connection))
            await otherRequest.Products.Where(item => item.Id == product.Id).ExecuteDeleteAsync();

        var controller = new ProductController(context);
        var result = delete
            ? await controller.DeleteConfirmed(product.Id)
            : await controller.Edit(product.Id, new ProductModel { Name = "Tên đã sửa", Price = 2000 });

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await context.Products.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Employee_delete_returns_not_found_when_another_request_deleted_the_record()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Employees.Add(new EmployeeModel { EmpId = "NV01", FullName = "Nhân viên thử" });
        await context.SaveChangesAsync();

        await using (var otherRequest = CreateContext(connection))
            await otherRequest.Employees.Where(item => item.EmpId == "NV01").ExecuteDeleteAsync();

        var result = await new EmployeeController(context).DeleteConfirmed("NV01");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await context.Employees.AsNoTracking().ToListAsync());
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
