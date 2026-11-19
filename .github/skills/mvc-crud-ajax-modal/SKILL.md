---
name: mvc-crud-ajax-modal
description: Tạo hoặc chuyển đổi chức năng CRUD của một entity trong ASP.NET Core MVC sang dạng Modal + Ajax (không reload trang). Dùng khi người dùng yêu cầu "sinh CRUD", "làm CRUD bằng modal/ajax" cho một entity.
---

# Skill: CRUD Modal + Ajax

## Đầu vào cần xác định
Tên entity (vd `SinhVien`), tên ViewModel (nếu có), danh sách trường, các quan hệ khoá ngoại (cần dropdown).

## Cấu trúc file sinh ra
```
Controllers/SinhVienController.cs
Views/SinhVien/Index.cshtml          // khung trang + nút Thêm + vùng bảng + modal rỗng
Views/SinhVien/_Table.cshtml         // bảng danh sách + phân trang (xem skill pagination-search)
Views/SinhVien/_Form.cshtml          // form dùng chung Create/Edit (trong modal)
Views/SinhVien/_Details.cshtml
Views/SinhVien/_Delete.cshtml
wwwroot/js/crud-modal.js             // JS dùng chung (viết 1 lần, mọi entity dùng lại)
```

## Controller mẫu
```csharp
[Authorize]
public class SinhVienController : Controller
{
    private readonly AppDbContext _db;
    public SinhVienController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var data = await BuildListAsync(q, page);
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_Table", data);
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdownsAsync();
        return PartialView("_Form", new SinhVien());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SinhVien model)
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return PartialView("_Form", model);          // trả lại form kèm lỗi
        }
        _db.Add(model);
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Thêm mới thành công" });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var m = await _db.SinhViens.FindAsync(id);
        if (m == null) return NotFound();
        await LoadDropdownsAsync(m);
        return PartialView("_Form", m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SinhVien model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) { await LoadDropdownsAsync(model); return PartialView("_Form", model); }
        try
        {
            _db.Update(model);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Json(new { success = false, message = "Dữ liệu đã bị thay đổi bởi người khác, hãy tải lại." });
        }
        return Json(new { success = true, message = "Cập nhật thành công" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await _db.SinhViens.FindAsync(id);
        if (m == null) return Json(new { success = false, message = "Không tìm thấy bản ghi" });
        try { _db.Remove(m); await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        { return Json(new { success = false, message = "Không thể xoá vì dữ liệu đang được sử dụng." }); }
        return Json(new { success = true, message = "Đã xoá" });
    }
}
```

## JS dùng chung (`wwwroot/js/crud-modal.js`)
```javascript
(function () {
  const modalEl = document.getElementById('crudModal');
  const modal = new bootstrap.Modal(modalEl);
  const body = modalEl.querySelector('.modal-content');

  function reloadTable() {
    const url = document.getElementById('table-container').dataset.url + window.location.search;
    return fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(r => r.text()).then(html => document.getElementById('table-container').innerHTML = html);
  }

  // Mở modal (Create/Edit/Details/Delete)
  document.addEventListener('click', async e => {
    const btn = e.target.closest('[data-modal-url]');
    if (!btn) return;
    const html = await (await fetch(btn.dataset.modalUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })).text();
    body.innerHTML = html;
    if (window.jQuery && jQuery.validator) jQuery.validator.unobtrusive.parse(body);
    modal.show();
  });

  // Gửi form trong modal bằng Ajax
  document.addEventListener('submit', async e => {
    const form = e.target.closest('#crudModal form');
    if (!form) return;
    e.preventDefault();
    const res = await fetch(form.action, { method: 'POST', body: new FormData(form),
      headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    const ct = res.headers.get('content-type') || '';
    if (ct.includes('application/json')) {
      const r = await res.json();
      if (r.success) { modal.hide(); await reloadTable(); showToast(r.message, 'success'); }
      else showToast(r.message, 'danger');
    } else {                          // server trả lại form kèm lỗi validation
      body.innerHTML = await res.text();
      if (window.jQuery && jQuery.validator) jQuery.validator.unobtrusive.parse(body);
    }
  });
})();
```
(`showToast` là hàm hiển thị Bootstrap Toast, đặt trong `site.js`.)

## Quy tắc bắt buộc
1. Form modal luôn có anti-forgery token (dùng `<form asp-action>`).
2. Sau khi lưu thành công: đóng modal, nạp lại `_Table` (giữ nguyên trang & từ khoá tìm kiếm), hiện toast.
3. Lỗi validation: trả lại partial form để hiển thị lỗi ngay trong modal.
4. Entity có khoá ngoại: dropdown nạp qua `LoadDropdownsAsync`, hiển thị **tên** (vd tên Khoa) thay vì mã.
5. Xoá: có modal xác nhận, bắt `DbUpdateException` khi vướng khoá ngoại.
6. Layout có sẵn `<div class="modal fade" id="crudModal"><div class="modal-dialog modal-lg"><div class="modal-content"></div></div></div>` và `<div id="table-container" data-url="@Url.Action("Index")">`.

## Checklist hoàn thành
- [ ] Create / Edit / Details / Delete đều mở bằng modal
- [ ] Không có reload toàn trang khi thao tác
- [ ] Validation hiển thị trong modal
- [ ] Phân trang & tìm kiếm hoạt động qua Ajax
- [ ] Có `[Authorize]` và `[ValidateAntiForgeryToken]`
