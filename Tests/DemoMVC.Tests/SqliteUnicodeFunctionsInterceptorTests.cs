using System.Data.Common;
using DemoMVC.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DemoMVC.Tests;

public class SqliteUnicodeFunctionsInterceptorTests
{
    [Theory]
    [InlineData("địa")]
    [InlineData("ĐỊA")]
    [InlineData("Địa Chất")]
    public async Task ToLower_contains_matches_vietnamese_uppercase_text_when_ef_opens_the_connection(string term)
    {
        // DB in-memory dùng chung: EF tự mở/đóng kết nối của nó nên interceptor đăng ký qua OnConfiguring được gọi.
        var connectionString = $"Data Source=unicode-search-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureCreatedAsync(); // HasData seed có Khoa "Địa chất" (DC).

        var search = term.Trim().ToLowerInvariant(); // Giống cách controller chuẩn hóa từ khóa.
        var codes = await context.Departments
            .Where(d => d.DepartmentCode.ToLower().Contains(search) || d.DepartmentName.ToLower().Contains(search))
            .Select(d => d.DepartmentCode)
            .ToListAsync();

        Assert.Equal(["DC"], codes);
    }

    [Fact]
    public async Task ToUpper_comparison_folds_vietnamese_letters()
    {
        var connectionString = $"Data Source=unicode-upper-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureCreatedAsync();

        var found = await context.Departments.AnyAsync(d => d.DepartmentName.ToUpper() == "ĐỊA CHẤT");

        Assert.True(found);
    }

    [Fact]
    public async Task RegisterFunctions_overrides_builtin_lower_and_upper_and_keeps_null()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        SqliteUnicodeFunctionsInterceptor.RegisterFunctions(connection);

        Assert.Equal("địa chất", await ScalarAsync(connection, "SELECT lower('ĐỊA CHẤT')"));
        Assert.Equal("ĐỊA CHẤT", await ScalarAsync(connection, "SELECT upper('địa chất')"));
        Assert.Equal(DBNull.Value, await ScalarAsync(connection, "SELECT lower(NULL)"));
    }

    [Fact]
    public async Task RegisterFunctions_supports_a_connection_opened_before_ef()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        SqliteUnicodeFunctionsInterceptor.RegisterFunctions(connection);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var found = await context.Departments.AnyAsync(d => d.DepartmentName.ToLower().Contains("địa"));

        Assert.True(found);
    }

    [Fact]
    public async Task Interceptor_ignores_non_sqlite_connections()
    {
        var interceptor = SqliteUnicodeFunctionsInterceptor.Instance;
        var otherConnection = new Mock<DbConnection>().Object;

        interceptor.ConnectionOpened(otherConnection, null!);
        await interceptor.ConnectionOpenedAsync(otherConnection, null!);
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
