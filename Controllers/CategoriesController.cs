
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly WarehouseDbContext _context;

        public CategoriesController(WarehouseDbContext context)
        {
            _context = context;
        }

        // GHI NHẬT KÝ HOẠT ĐỘNG
        private void AddActivityLog(string action, string description)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = action,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        // DANH SÁCH DANH MỤC
        public async Task<IActionResult> Index(string? searchString)
        {
            var categories = _context.Categories.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string keyword = searchString.Trim();

                categories = categories.Where(c =>
                    c.Name.Contains(keyword));
            }

            ViewBag.SearchString = searchString;

            return View(await categories
                .OrderBy(c => c.Name)
                .ToListAsync());
        }

        // FORM THÊM
        public IActionResult Create()
        {
            return View();
        }

        // XỬ LÝ THÊM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);

                AddActivityLog(
                    "Thêm danh mục",
                    $"Đã thêm danh mục: {category.Name}."
                );

                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm danh mục thành công!";

                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // FORM SỬA
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories.FindAsync(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // XỬ LÝ SỬA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category input)
        {
            if (id != input.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                    return NotFound();

                string oldName = category.Name;

                category.Name = input.Name;
                category.Description = input.Description;

                AddActivityLog(
                    "Sửa danh mục",
                    $"Đã cập nhật danh mục ID {id}: {oldName} → {category.Name}."
                );

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật danh mục thành công!";

                return RedirectToAction(nameof(Index));
            }

            return View(input);
        }

        // FORM XÁC NHẬN XÓA
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // XỬ LÝ XÓA
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category != null)
            {
                string categoryName = category.Name;

                _context.Categories.Remove(category);

                AddActivityLog(
                    "Xóa danh mục",
                    $"Đã xóa danh mục: {categoryName} (ID: {id})."
                );

                await _context.SaveChangesAsync();

                TempData["Success"] = "Xóa danh mục thành công!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
