
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class WarehouseShelvesController : Controller
    {
        private readonly WarehouseDbContext _context;

        public WarehouseShelvesController(WarehouseDbContext context)
        {
            _context = context;
        }

        // Xem danh sách kệ của một khu vực
        public async Task<IActionResult> Index(int areaId)
        {
            var area = await _context.WarehouseAreas
                .Include(a => a.Warehouse)
                .Include(a => a.Shelves)
                .FirstOrDefaultAsync(a => a.Id == areaId);

            if (area == null)
                return NotFound();

            ViewBag.Area = area;
            return View(area.Shelves);
        }

        // Thêm kệ
        public async Task<IActionResult> Create(int areaId)
        {
            var area = await _context.WarehouseAreas
                .Include(a => a.Warehouse)
                .FirstOrDefaultAsync(a => a.Id == areaId);

            if (area == null)
                return NotFound();

            ViewBag.AreaName = area.Name;

            return View(new WarehouseShelf
            {
                WarehouseAreaId = areaId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseShelf shelf)
        {
            var area = await _context.WarehouseAreas
                .FirstOrDefaultAsync(a => a.Id == shelf.WarehouseAreaId);

            if (area == null)
                return NotFound();

            if (await _context.WarehouseShelves.AnyAsync(s =>
                s.WarehouseAreaId == shelf.WarehouseAreaId &&
                s.Code == shelf.Code))
            {
                ModelState.AddModelError("Code", "Mã kệ đã tồn tại trong khu vực.");
            }

            if (ModelState.IsValid)
            {
                _context.WarehouseShelves.Add(shelf);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", new
                {
                    areaId = shelf.WarehouseAreaId
                });
            }

            ViewBag.AreaName = area.Name;
            return View(shelf);
        }

        // Sửa kệ
        public async Task<IActionResult> Edit(int id)
        {
            var shelf = await _context.WarehouseShelves
                .Include(s => s.WarehouseArea)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shelf == null)
                return NotFound();

            ViewBag.AreaName = shelf.WarehouseArea?.Name;
            return View(shelf);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WarehouseShelf shelf)
        {
            if (id != shelf.Id)
                return NotFound();

            var existing = await _context.WarehouseShelves
                .FirstOrDefaultAsync(s => s.Id == id);

            if (existing == null)
                return NotFound();

            if (await _context.WarehouseShelves.AnyAsync(s =>
                s.WarehouseAreaId == existing.WarehouseAreaId &&
                s.Code == shelf.Code &&
                s.Id != id))
            {
                ModelState.AddModelError("Code", "Mã kệ đã tồn tại trong khu vực.");
            }

            if (ModelState.IsValid)
            {
                existing.Code = shelf.Code;
                existing.Name = shelf.Name;

                await _context.SaveChangesAsync();

                return RedirectToAction("Index", new
                {
                    areaId = existing.WarehouseAreaId
                });
            }

            ViewBag.AreaName = await _context.WarehouseAreas
                .Where(a => a.Id == existing.WarehouseAreaId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync();

            return View(shelf);
        }

        // Xóa kệ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var shelf = await _context.WarehouseShelves
                .Include(s => s.Bins)
                    .ThenInclude(b => b.ProductBatches)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shelf == null)
                return NotFound();

            bool hasBatches = shelf.Bins.Any(b => b.ProductBatches.Any());

            if (hasBatches)
            {
                TempData["Error"] = "Không thể xóa kệ đang chứa lô hàng.";
                return RedirectToAction("Index", new
                {
                    areaId = shelf.WarehouseAreaId
                });
            }

            int areaId = shelf.WarehouseAreaId;

            _context.WarehouseShelves.Remove(shelf);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { areaId });
        }
    }
}
