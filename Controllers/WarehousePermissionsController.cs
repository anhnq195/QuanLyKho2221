
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin")]
    public class WarehousePermissionsController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public WarehousePermissionsController(
            WarehouseDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // DANH SÁCH PHÂN QUYỀN
        public async Task<IActionResult> Index()
        {
            var permissions = await _context.WarehouseUserPermissions
                .Include(p => p.User)
                .Include(p => p.Warehouse)
                .OrderBy(p => p.Warehouse!.Code)
                .ThenBy(p => p.User!.UserName)
                .ToListAsync();

            return View(permissions);
        }

        // NẠP DANH SÁCH NHÂN VIÊN VÀ KHO
        private async Task LoadDropdowns()
        {
            var users = new List<IdentityUser>();

            foreach (var user in await _userManager.Users.ToListAsync())
            {
                if (await _userManager.IsInRoleAsync(user, "Employee"))
                {
                    users.Add(user);
                }
            }

            ViewBag.Users = new SelectList(
                users,
                "Id",
                "UserName"
            );

            var warehouses = await _context.Warehouses
                .Where(w => w.IsActive)
                .OrderBy(w => w.Code)
                .ToListAsync();

            ViewBag.Warehouses = new SelectList(
                warehouses,
                "Id",
                "Name"
            );
        }

        // THÊM PHÂN QUYỀN - GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View(new WarehousePermissionViewModel());
        }

        // THÊM PHÂN QUYỀN - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            WarehousePermissionViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null ||
                !await _userManager.IsInRoleAsync(user, "Employee"))
            {
                ModelState.AddModelError(
                    "UserId", "Nhân viên không hợp lệ.");
            }

            bool warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == model.WarehouseId && w.IsActive);

            if (!warehouseExists)
            {
                ModelState.AddModelError(
                    "WarehouseId", "Kho không tồn tại hoặc đã ngừng hoạt động.");
            }

            bool duplicate = await _context.WarehouseUserPermissions
                .AnyAsync(p =>
                    p.UserId == model.UserId &&
                    p.WarehouseId == model.WarehouseId);

            if (duplicate)
            {
                ModelState.AddModelError(
                    "", "Nhân viên đã được phân quyền tại kho này.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns();
                return View(model);
            }

            var permission = new WarehouseUserPermission
            {
                UserId = model.UserId,
                WarehouseId = model.WarehouseId,
                CanView = model.CanView,
                CanImport = model.CanImport,
                CanExport = model.CanExport,
                CanManage = model.CanManage
            };

            _context.WarehouseUserPermissions.Add(permission);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Phân quyền nhân viên thành công!";

            return RedirectToAction(nameof(Index));
        }

        // SỬA PHÂN QUYỀN - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var permission = await _context.WarehouseUserPermissions
                .Include(p => p.User)
                .Include(p => p.Warehouse)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (permission == null)
                return NotFound();

            var model = new WarehousePermissionViewModel
            {
                Id = permission.Id,
                UserId = permission.UserId,
                UserName = permission.User?.UserName ?? "",
                WarehouseId = permission.WarehouseId,
                WarehouseName = permission.Warehouse?.Name ?? "",
                CanView = permission.CanView,
                CanImport = permission.CanImport,
                CanExport = permission.CanExport,
                CanManage = permission.CanManage
            };

            await LoadDropdowns();

            return View(model);
        }

        // SỬA PHÂN QUYỀN - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            WarehousePermissionViewModel model)
        {
            var permission = await _context.WarehouseUserPermissions
                .FirstOrDefaultAsync(p => p.Id == model.Id);

            if (permission == null)
                return NotFound();

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null ||
                !await _userManager.IsInRoleAsync(user, "Employee"))
            {
                ModelState.AddModelError(
                    "UserId", "Nhân viên không hợp lệ.");
            }

            bool warehouseExists = await _context.Warehouses
                .AnyAsync(w => w.Id == model.WarehouseId && w.IsActive);

            if (!warehouseExists)
            {
                ModelState.AddModelError(
                    "WarehouseId", "Kho không tồn tại hoặc đã ngừng hoạt động.");
            }

            bool duplicate = await _context.WarehouseUserPermissions
                .AnyAsync(p =>
                    p.Id != model.Id &&
                    p.UserId == model.UserId &&
                    p.WarehouseId == model.WarehouseId);

            if (duplicate)
            {
                ModelState.AddModelError(
                    "", "Nhân viên đã có phân quyền tại kho này.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns();
                return View(model);
            }

            permission.UserId = model.UserId;
            permission.WarehouseId = model.WarehouseId;
            permission.CanView = model.CanView;
            permission.CanImport = model.CanImport;
            permission.CanExport = model.CanExport;
            permission.CanManage = model.CanManage;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật phân quyền thành công!";

            return RedirectToAction(nameof(Index));
        }

        // XÓA PHÂN QUYỀN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var permission = await _context.WarehouseUserPermissions
                .FirstOrDefaultAsync(p => p.Id == id);

            if (permission == null)
                return NotFound();

            _context.WarehouseUserPermissions.Remove(permission);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa phân quyền thành công!";

            return RedirectToAction(nameof(Index));
        }
    }
}
