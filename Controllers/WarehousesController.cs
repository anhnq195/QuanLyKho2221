
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class WarehousesController : Controller
    {
        private readonly WarehouseDbContext _context;

        public WarehousesController(WarehouseDbContext context)
        {
            _context = context;
        }

        // Danh sách kho
        public async Task<IActionResult> Index()
        {
            var warehouses = await _context.Warehouses
                .Include(w => w.Areas)
                .OrderBy(w => w.Code)
                .ToListAsync();

            return View(warehouses);
        }

        // Chi tiết kho
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var warehouse = await _context.Warehouses
                .Include(w => w.Areas)
                    .ThenInclude(a => a.Shelves)
                        .ThenInclude(s => s.Bins)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (warehouse == null)
                return NotFound();

            return View(warehouse);
        }

        // Hiển thị form thêm kho
        public IActionResult Create()
        {
            return View();
        }

        // Lưu kho mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Warehouse warehouse)
        {
            if (await _context.Warehouses.AnyAsync(
                w => w.Code == warehouse.Code))
            {
                ModelState.AddModelError(
                    "Code", "Mã kho đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                warehouse.CreatedAt = DateTime.Now;

                _context.Warehouses.Add(warehouse);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm kho mới thành công!";

                return RedirectToAction(nameof(Index));
            }

            return View(warehouse);
        }

        // Hiển thị form sửa kho
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var warehouse = await _context.Warehouses.FindAsync(id);

            if (warehouse == null)
                return NotFound();

            return View(warehouse);
        }

        // Lưu thông tin kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id, Warehouse warehouse)
        {
            if (id != warehouse.Id)
                return NotFound();

            if (await _context.Warehouses.AnyAsync(
                w => w.Code == warehouse.Code && w.Id != id))
            {
                ModelState.AddModelError(
                    "Code", "Mã kho đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.Warehouses
                    .FindAsync(id);

                if (existing == null)
                    return NotFound();

                existing.Code = warehouse.Code;
                existing.Name = warehouse.Name;
                existing.Address = warehouse.Address;
                existing.ManagerName = warehouse.ManagerName;
                existing.Description = warehouse.Description;
                existing.IsActive = warehouse.IsActive;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật thông tin kho thành công!";

                return RedirectToAction(nameof(Index));
            }

            return View(warehouse);
        }

        // Hiển thị xác nhận xóa kho
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.Id == id);

            if (warehouse == null)
                return NotFound();

            return View(warehouse);
        }

        // Xóa kho
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var warehouse = await _context.Warehouses
                .Include(w => w.Areas)
                    .ThenInclude(a => a.Shelves)
                        .ThenInclude(s => s.Bins)
                            .ThenInclude(b => b.ProductBatches)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (warehouse == null)
                return NotFound();

            bool hasBatches = warehouse.Areas
                .SelectMany(a => a.Shelves)
                .SelectMany(s => s.Bins)
                .Any(b => b.ProductBatches.Any());

            if (hasBatches)
            {
                TempData["Error"] =
                    "Không thể xóa kho đang chứa lô hàng.";

                return RedirectToAction(nameof(Index));
            }

            _context.Warehouses.Remove(warehouse);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa kho thành công!";

            return RedirectToAction(nameof(Index));
        }
    }
}
