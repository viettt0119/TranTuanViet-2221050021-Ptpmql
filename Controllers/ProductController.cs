using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;
namespace DemoMVC.Controllers
{

    using Microsoft.AspNetCore.Mvc;

    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
    {
        _context = context; 
    }
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
        {
            var productsQuery = _context.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim().ToLowerInvariant();
                productsQuery = productsQuery.Where(product =>
                    product.Name != null && product.Name.ToLower().Contains(search));
            }

            var products = await PagedList<ProductModel>.CreateAsync(
                productsQuery.OrderBy(product => product.Id),
                page,
                pageSize,
                HttpContext.RequestAborted);
            ViewData["SearchQuery"] = q?.Trim();
            return View(products);
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products.FindAsync(id);
            return product == null ? NotFound() : PartialView("_DetailsModal", product);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["ModalTitle"] = "Thêm sản phẩm";
            ViewData["FormActionUrl"] = Url.Action(nameof(Create));
            return PartialView("_FormModal", new ProductModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Price")] ProductModel product)
        {
            if (!ModelState.IsValid)
                return FormModal(product, nameof(Create), "Thêm sản phẩm");

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã thêm sản phẩm." });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["ModalTitle"] = "Sửa sản phẩm";
            ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id });
            return PartialView("_FormModal", product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Name,Price")] ProductModel form)
        {
            if (!ModelState.IsValid)
                return FormModal(form, nameof(Edit), "Sửa sản phẩm", new { id });

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = form.Name;
            product.Price = form.Price;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã cập nhật sản phẩm." });
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
            return PartialView("_DeleteModal", product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa sản phẩm." });
        }

        private PartialViewResult FormModal(
            ProductModel product,
            string action,
            string title,
            object? routeValues = null)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            ViewData["ModalTitle"] = title;
            ViewData["FormActionUrl"] = Url.Action(action, routeValues);
            return PartialView("_FormModal", product);
        }
    }
}
