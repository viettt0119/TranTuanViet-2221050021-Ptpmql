using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public class PagedListTests
{
    [Fact]
    public async Task CreateAsync_returns_requested_page_and_navigation_state()
    {
        await using var connection = await CreateDatabaseAsync(25);
        await using var context = CreateContext(connection);

        var result = await PagedList<ProductModel>.CreateAsync(context.Products.OrderBy(p => p.Id), 2, 10);

        Assert.Equal(2, result.PageIndex);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalRecords);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(11, result.Items[0].Id);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task CreateAsync_clamps_page_above_last_page()
    {
        await using var connection = await CreateDatabaseAsync(25);
        await using var context = CreateContext(connection);

        var result = await PagedList<ProductModel>.CreateAsync(context.Products.OrderBy(p => p.Id), 99, 10);

        Assert.Equal(3, result.PageIndex);
        Assert.Equal(5, result.Items.Count);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(15)]
    public async Task CreateAsync_uses_default_size_for_unsupported_page_size(int requestedPageSize)
    {
        await using var connection = await CreateDatabaseAsync(25);
        await using var context = CreateContext(connection);

        var result = await PagedList<ProductModel>.CreateAsync(
            context.Products.OrderBy(p => p.Id),
            -1,
            requestedPageSize);

        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(10, result.Items.Count);
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    public async Task CreateAsync_accepts_available_page_sizes(int pageSize)
    {
        await using var connection = await CreateDatabaseAsync(25);
        await using var context = CreateContext(connection);

        var result = await PagedList<ProductModel>.CreateAsync(context.Products, 1, pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 25), result.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_handles_empty_source()
    {
        await using var connection = await CreateDatabaseAsync(0);
        await using var context = CreateContext(connection);

        var result = await PagedList<ProductModel>.CreateAsync(context.Products, 4, 10);

        Assert.Equal(1, result.PageIndex);
        Assert.Equal(0, result.TotalRecords);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    private static async Task<SqliteConnection> CreateDatabaseAsync(int productCount)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        for (var id = 1; id <= productCount; id++)
        {
            context.Products.Add(new ProductModel { Id = id, Name = $"Product {id}", Price = 1000 });
        }
        await context.SaveChangesAsync();
        return connection;
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }
}
