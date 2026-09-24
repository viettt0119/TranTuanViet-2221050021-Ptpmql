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
        public async Task<IActionResult> Index()
        {
            // Truy vấn lấy toàn bộ dữ liệu từ bảng Products
            var products = await _context.Products.ToListAsync();
            return View(products); // Truyền dữ liệu sang View
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(ProductModel product)
        {
            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                _context.SaveChanges();

                return RedirectToAction("Index"); 
            }
            return View(product);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (id == 0) return NotFound();
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        return View(product); 
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductModel product)
    {
    if (id != product.Id) return NotFound();

    if (ModelState.IsValid)
    {
        _context.Update(product);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    return View(product);
}
    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
    if (id == null) return NotFound();

    var product = await _context.Products.FindAsync(id);
    if (product == null) return NotFound();

    return View(product); 
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
    var product = await _context.Products.FindAsync(id);
    if (product != null)
    {
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
    }
    return RedirectToAction(nameof(Index));
    }
    }
}
