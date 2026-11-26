using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DemoMVC.Data;

/// <summary>
/// Hàm <c>lower()</c>/<c>upper()</c> có sẵn của SQLite chỉ đổi hoa/thường cho ký tự ASCII, nên
/// <c>lower('Địa chất')</c> vẫn giữ chữ "Đ" và tìm kiếm "địa" không bao giờ khớp. Interceptor này ghi đè
/// hai hàm đó bằng <see cref="string.ToLowerInvariant"/>/<see cref="string.ToUpperInvariant"/> của .NET
/// mỗi khi EF Core mở kết nối SQLite, nhờ vậy truy vấn <c>x.ToLower().Contains(q)</c> hỗ trợ tiếng Việt.
/// </summary>
/// <remarks>
/// Không làm gì với kết nối không phải SQLite. Nếu tự truyền một <see cref="SqliteConnection"/> đã mở sẵn
/// cho EF Core (EF sẽ không mở lại nên interceptor không chạy), hãy gọi <see cref="RegisterFunctions"/> thủ công.
/// </remarks>
public sealed class SqliteUnicodeFunctionsInterceptor : DbConnectionInterceptor
{
    /// <summary>Interceptor không có trạng thái nên dùng chung một thể hiện cho mọi DbContext.</summary>
    public static SqliteUnicodeFunctionsInterceptor Instance { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is SqliteConnection sqliteConnection)
            RegisterFunctions(sqliteConnection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqliteConnection)
            RegisterFunctions(sqliteConnection);
        return Task.CompletedTask;
    }

    /// <summary>Ghi đè hàm SQL <c>lower</c> và <c>upper</c> (1 tham số) trên kết nối đã cho.</summary>
    public static void RegisterFunctions(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.CreateFunction<string?, string?>("lower", value => value?.ToLowerInvariant(), isDeterministic: true);
        connection.CreateFunction<string?, string?>("upper", value => value?.ToUpperInvariant(), isDeterministic: true);
    }
}
