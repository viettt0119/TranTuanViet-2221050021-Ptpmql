using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoMVC.Data;
using DemoMVC.Models;

namespace DemoMVC.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Employees.OrderBy(employee => employee.EmpId).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var employeeModel = await _context.Employees
                .FirstOrDefaultAsync(m => m.EmpId == id);
            if (employeeModel == null)
                return NotFound();
            return PartialView("_DetailsModal", employeeModel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["ModalTitle"] = "Thêm nhân viên";
            ViewData["FormActionUrl"] = Url.Action(nameof(Create));
            ViewData["IsCreate"] = true;
            return PartialView("_FormModal", new EmployeeModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EmpId,FullName,HireDate")] EmployeeModel employeeModel)
        {
            if (ModelState.IsValid)
            {
                _context.Add(employeeModel);
                try
                {
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Đã thêm nhân viên." });
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Không thể lưu nhân viên. Kiểm tra mã nhân viên có bị trùng không.");
                }
            }
            return FormModal(employeeModel, nameof(Create), "Thêm nhân viên", true);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var employeeModel = await _context.Employees.FindAsync(id);
            if (employeeModel == null)
                return NotFound();
            ViewData["ModalTitle"] = "Sửa nhân viên";
            ViewData["FormActionUrl"] = Url.Action(nameof(Edit), new { id });
            ViewData["IsCreate"] = false;
            return PartialView("_FormModal", employeeModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("EmpId,FullName,HireDate")] EmployeeModel form)
        {
            if (id != form.EmpId) return BadRequest();
            if (!ModelState.IsValid)
                return FormModal(form, nameof(Edit), "Sửa nhân viên", false, new { id });

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();
            employee.FullName = form.FullName;
            employee.HireDate = form.HireDate;
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật nhân viên." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Không thể cập nhật nhân viên.");
                return FormModal(form, nameof(Edit), "Sửa nhân viên", false, new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var employeeModel = await _context.Employees.FindAsync(id);
            if (employeeModel == null)
                return NotFound();
            ViewData["FormActionUrl"] = Url.Action(nameof(Delete), new { id });
            return PartialView("_DeleteModal", employeeModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var employeeModel = await _context.Employees.FindAsync(id);
            if (employeeModel == null) return NotFound();
            _context.Employees.Remove(employeeModel);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa nhân viên." });
        }

        private PartialViewResult FormModal(
            EmployeeModel employee,
            string action,
            string title,
            bool isCreate,
            object? routeValues = null)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            ViewData["ModalTitle"] = title;
            ViewData["FormActionUrl"] = Url.Action(action, routeValues);
            ViewData["IsCreate"] = isCreate;
            return PartialView("_FormModal", employee);
        }
    }
}
