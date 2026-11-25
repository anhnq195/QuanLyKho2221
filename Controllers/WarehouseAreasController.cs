
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class WarehouseAreasController : Controller
    {
        private readonly WarehouseDbContext _context;

        public WarehouseAreasController(WarehouseDbContext context)
        {
            _context = context;
        }

        // DANH SÁCH KHU VỰC
        public async Task<IActionResult> Index(int warehouseId)
        {
            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == warehouseId);

            if (warehouse == null)
                return NotFound("Không tìm thấy kho hàng.");

            var areas = await _context.WarehouseAreas
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .Include(a => a.Shelves)
                .Where(a => a.WarehouseId == warehouseId)
                .OrderBy(a => a.Code)
                .ToListAsync();

            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseName = warehouse.Name;

            return View(areas);
        }

        // HIỂN THỊ FORM THÊM
        public async Task<IActionResult> Create(int warehouseId)
        {
            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == warehouseId);

            if (warehouse == null)
                return NotFound("Không tìm thấy kho hàng.");

            ViewBag.WarehouseName = warehouse.Name;

            return View(new WarehouseArea
            {
                WarehouseId = warehouseId
            });
        }

        // XỬ LÝ THÊM KHU VỰC
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseArea area)
        {
            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.Id == area.WarehouseId);

            if (warehouse == null)
                return NotFound("Không tìm thấy kho hàng.");

            area.Code = area.Code?.Trim() ?? "";
            area.Name = area.Name?.Trim() ?? "";

            if (await _context.WarehouseAreas.AnyAsync(a =>
                a.WarehouseId == area.WarehouseId &&
                a.Code == area.Code))
            {
                ModelState.AddModelError(
                    nameof(area.Code),
                    "Mã khu vực đã tồn tại trong kho này.");
            }

            if (ModelState.IsValid)
            {
                var newArea = new WarehouseArea
                {
                    WarehouseId = area.WarehouseId,
                    Code = area.Code,
                    Name = area.Name
                };

                _context.WarehouseAreas.Add(newArea);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm khu vực kho thành công.";

                return RedirectToAction(
                    "Details",
                    "Warehouses",
                    new { id = area.WarehouseId });
            }

            ViewBag.WarehouseName = warehouse.Name;

            return View(area);
        }

        // HIỂN THỊ FORM SỬA
        public async Task<IActionResult> Edit(int id)
        {
            var area = await _context.WarehouseAreas
                .Include(a => a.Warehouse)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (area == null)
                return NotFound("Không tìm thấy khu vực.");

            ViewBag.WarehouseName = area.Warehouse?.Name;

            return View(area);
        }

        // XỬ LÝ SỬA KHU VỰC
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WarehouseArea area)
        {
            if (id != area.Id)
                return NotFound();

            var existing = await _context.WarehouseAreas
                .FirstOrDefaultAsync(a => a.Id == id);

            if (existing == null)
                return NotFound("Không tìm thấy khu vực.");

            // Không cho thay đổi kho chứa khu vực
            area.WarehouseId = existing.WarehouseId;

            area.Code = area.Code?.Trim() ?? "";
            area.Name = area.Name?.Trim() ?? "";

            if (await _context.WarehouseAreas.AnyAsync(a =>
                a.WarehouseId == existing.WarehouseId &&
                a.Code == area.Code &&
                a.Id != id))
            {
                ModelState.AddModelError(
                    nameof(area.Code),
                    "Mã khu vực đã tồn tại trong kho này.");
            }

            if (ModelState.IsValid)
            {
                existing.Code = area.Code;
                existing.Name = area.Name;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật khu vực thành công.";

                return RedirectToAction(
                    "Details",
                    "Warehouses",
                    new { id = existing.WarehouseId });
            }

            ViewBag.WarehouseName = await _context.Warehouses
                .Where(w => w.Id == existing.WarehouseId)
                .Select(w => w.Name)
                .FirstOrDefaultAsync();

            return View(area);
        }

        // XÓA KHU VỰC
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var area = await _context.WarehouseAreas
                .Include(a => a.Shelves)
                    .ThenInclude(s => s.Bins)
                        .ThenInclude(b => b.ProductBatches)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (area == null)
                return NotFound("Không tìm thấy khu vực.");

            int warehouseId = area.WarehouseId;

            bool hasBatches = area.Shelves
                .SelectMany(s => s.Bins)
                .Any(b => b.ProductBatches.Any());

            if (hasBatches)
            {
                TempData["Error"] =
                    "Không thể xóa khu vực vì vẫn còn lô hàng đang được lưu trữ.";

                return RedirectToAction(
                    "Details",
                    "Warehouses",
                    new { id = warehouseId });
            }

            if (area.Shelves.Any())
            {
                TempData["Error"] =
                    "Không thể xóa khu vực vì vẫn còn kệ hàng bên trong. Hãy xóa hoặc chuyển các kệ trước.";

                return RedirectToAction(
                    "Details",
                    "Warehouses",
                    new { id = warehouseId });
            }

            _context.WarehouseAreas.Remove(area);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa khu vực kho thành công.";

            return RedirectToAction(
                "Details",
                "Warehouses",
                new { id = warehouseId });
        }
    }
}
