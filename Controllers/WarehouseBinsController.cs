
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class WarehouseBinsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public WarehouseBinsController(WarehouseDbContext context)
        {
            _context = context;
        }

        // Danh sách ô chứa của một kệ
        public async Task<IActionResult> Index(int shelfId)
        {
            var shelf = await _context.WarehouseShelves
                .Include(s => s.WarehouseArea)
                .Include(s => s.Bins)
                    .ThenInclude(b => b.ProductBatches)
                .FirstOrDefaultAsync(s => s.Id == shelfId);

            if (shelf == null)
                return NotFound();

            ViewBag.Shelf = shelf;

            return View(shelf.Bins.OrderBy(b => b.Code).ToList());
        }

        // Trang thêm ô chứa
        public async Task<IActionResult> Create(int shelfId)
        {
            var shelf = await _context.WarehouseShelves
                .FirstOrDefaultAsync(s => s.Id == shelfId);

            if (shelf == null)
                return NotFound();

            ViewBag.ShelfName = shelf.Name;

            return View(new WarehouseBin
            {
                WarehouseShelfId = shelfId,
                FloorNumber = 1
            });
        }

        // Xử lý thêm ô chứa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseBin bin)
        {
            var shelf = await _context.WarehouseShelves
                .FirstOrDefaultAsync(s => s.Id == bin.WarehouseShelfId);

            if (shelf == null)
                return NotFound();

            if (await _context.WarehouseBins.AnyAsync(b =>
                b.WarehouseShelfId == bin.WarehouseShelfId &&
                b.Code == bin.Code))
            {
                ModelState.AddModelError(
                    "Code",
                    "Mã ô chứa đã tồn tại trong kệ."
                );
            }

            if (ModelState.IsValid)
            {
                _context.WarehouseBins.Add(bin);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm ô chứa thành công!";

                return RedirectToAction(nameof(Index), new
                {
                    shelfId = bin.WarehouseShelfId
                });
            }

            ViewBag.ShelfName = shelf.Name;

            return View(bin);
        }

        // Trang sửa ô chứa
        public async Task<IActionResult> Edit(int id)
        {
            var bin = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bin == null)
                return NotFound();

            ViewBag.ShelfName = bin.WarehouseShelf?.Name;

            return View(bin);
        }

        // Xử lý sửa ô chứa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WarehouseBin bin)
        {
            if (id != bin.Id)
                return NotFound();

            var existing = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existing == null)
                return NotFound();

            if (await _context.WarehouseBins.AnyAsync(b =>
                b.WarehouseShelfId == existing.WarehouseShelfId &&
                b.Code == bin.Code &&
                b.Id != id))
            {
                ModelState.AddModelError(
                    "Code",
                    "Mã ô chứa đã tồn tại trong kệ."
                );
            }

            if (ModelState.IsValid)
            {
                // Cập nhật mã, tên, tầng và sức chứa tối đa
                // Giữ nguyên kệ ban đầu
                existing.Code = bin.Code;
                existing.Name = bin.Name;
                existing.FloorNumber = bin.FloorNumber;
                existing.MaxCapacity = bin.MaxCapacity > 0 ? bin.MaxCapacity : 1000;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật ô chứa thành công!";

                return RedirectToAction(nameof(Index), new
                {
                    shelfId = existing.WarehouseShelfId
                });
            }

            ViewBag.ShelfName = existing.WarehouseShelf?.Name;

            return View(bin);
        }

        // Xóa ô chứa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var bin = await _context.WarehouseBins
                .Include(b => b.ProductBatches)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bin == null)
                return NotFound();

            if (bin.ProductBatches.Any())
            {
                TempData["Error"] =
                    "Không thể xóa ô chứa đang có lô hàng.";

                return RedirectToAction(nameof(Index), new
                {
                    shelfId = bin.WarehouseShelfId
                });
            }

            int shelfId = bin.WarehouseShelfId;

            _context.WarehouseBins.Remove(bin);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa ô chứa thành công!";

            return RedirectToAction(nameof(Index), new
            {
                shelfId
            });
        }
    }
}
