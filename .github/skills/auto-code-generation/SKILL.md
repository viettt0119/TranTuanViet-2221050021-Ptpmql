---
name: auto-code-generation
description: Sinh mã tự động có cấu trúc (vd mã sinh viên 2821050199 = năm nhập học + hệ đào tạo + ngành + số thứ tự 4 chữ số) an toàn khi nhiều người thêm cùng lúc. Dùng khi thêm chức năng tạo bản ghi cần mã tự sinh.
---

# Skill: Sinh mã tự động

## Quy tắc mã sinh viên (10 chữ số)
`[YY năm nhập học][HH hệ đào tạo][NN ngành][SSSS số thứ tự]`
- Ví dụ `2821050199`: 28 = năm nhập học (2028), 21 = đại học chính quy, 05 = Công nghệ thông tin, 0199 = thứ tự.
- Sinh viên kế tiếp cùng (năm, hệ, ngành) → `2821050200`.
- Số thứ tự tính **riêng cho từng tổ hợp** năm-hệ-ngành; bắt đầu `0001`.

## Cài đặt `Services/MaSinhVienGenerator.cs`
```csharp
public interface IMaSinhVienGenerator
{
    Task<string> NextAsync(int namNhapHoc, string maHe, string maNganh, CancellationToken ct = default);
}

public class MaSinhVienGenerator : IMaSinhVienGenerator
{
    private readonly AppDbContext _db;
    public MaSinhVienGenerator(AppDbContext db) => _db = db;

    public async Task<string> NextAsync(int namNhapHoc, string maHe, string maNganh, CancellationToken ct = default)
    {
        var prefix = $"{namNhapHoc % 100:D2}{maHe:D2}{maNganh:D2}";   // 6 ký tự; maHe/maNganh dạng "21", "05"
        var last = await _db.SinhViens
            .Where(s => s.MaSV.StartsWith(prefix))
            .OrderByDescending(s => s.MaSV)
            .Select(s => s.MaSV)
            .FirstOrDefaultAsync(ct);

        var next = last == null ? 1 : int.Parse(last[6..]) + 1;
        if (next > 9999) throw new InvalidOperationException($"Đã hết số thứ tự cho tiền tố {prefix}");
        return $"{prefix}{next:D4}";
    }
}
```
Đăng ký: `builder.Services.AddScoped<IMaSinhVienGenerator, MaSinhVienGenerator>();`

## Chống trùng khi thêm đồng thời
1. Bắt buộc có **unique index** trên `MaSV`.
2. Khi `SaveChangesAsync` ném `DbUpdateException` do trùng mã → sinh lại mã và thử lại (tối đa 3 lần).
3. Cách khác: bảng `BoDemMa(Prefix PK, GiaTriCuoi)` cập nhật trong transaction `Serializable`.

## Quy tắc
- Mã được sinh ở **server**, trường mã trong form để `readonly` (có thể hiển thị bản xem trước qua Ajax khi chọn năm/hệ/ngành).
- Không sinh mã ở client, không dùng `Count()+1` (sai khi có xoá/đồng thời).
- Khi import Excel: sinh mã theo từng dòng trong cùng transaction, theo dõi số đã cấp trong bộ nhớ để tránh trùng giữa các dòng chưa lưu.
- Viết unit test: mã đầu tiên `...0001`, mã kế tiếp tăng 1, tổ hợp khác độc lập, tràn 9999 báo lỗi.
- Cần bảng danh mục **Hệ đào tạo** và **Ngành** có trường mã 2 chữ số; mã phải cố định, không đổi sau khi đã dùng.
