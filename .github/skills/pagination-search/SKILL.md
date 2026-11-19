---
name: pagination-search
description: Thêm phân trang và tìm kiếm phía server cho trang danh sách trong ASP.NET Core MVC (EF Core), tương thích Ajax. Dùng khi tạo hoặc sửa bất kỳ trang danh sách nào.
---

# Skill: Phân trang & tìm kiếm

## Lớp dùng chung `Helpers/PagedResult.cs`
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    public string? Keyword { get; set; }

    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize, string? keyword = null)
    {
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalItems = total, Keyword = keyword };
    }
}
```

## Cách dùng trong controller
```csharp
var query = _db.SinhViens.AsNoTracking().Include(s => s.Khoa).AsQueryable();
if (!string.IsNullOrWhiteSpace(q))
    query = query.Where(s => s.MaSV.Contains(q) || s.HoTen.Contains(q) || s.Khoa.TenKhoa.Contains(q));
query = query.OrderBy(s => s.MaSV);                 // BẮT BUỘC có OrderBy trước Skip/Take
var data = await PagedResult<SinhVien>.CreateAsync(query, page, 10, q);
```

## Partial `_Pager.cshtml` (Bootstrap 5)
- Nhận `PagedResult<T>`, hiển thị Trước/Sau và các số trang (rút gọn nếu > 7 trang).
- Link phân trang mang `data-page` và giữ `q`; JS bắt click → fetch `Index?page=..&q=..` với header `X-Requested-With` → thay `#table-container`.
- Ô tìm kiếm: debounce 300ms, khi tìm luôn về `page=1`.

## Quy tắc
1. Áp dụng cho **TẤT CẢ** trang danh sách (kể cả Role, User, các danh mục).
2. Luôn `OrderBy` ổn định; cố định `PageSize` trong cấu hình, không nhận từ client không giới hạn (chặn > 100).
3. Tìm kiếm không phân biệt hoa thường theo collation CSDL; không dùng `ToLower()` trên cột nếu tránh được (làm mất index).
4. Không `ToList()` trước khi lọc/phân trang.
5. Khi xoá bản ghi cuối của trang, tự lùi về trang trước.
6. Hiển thị "Hiển thị x–y / tổng z bản ghi" và thông báo "Không có dữ liệu" khi rỗng.
