
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class ProductBatchesController : Controller
    {
        private readonly WarehouseDbContext _context;

        public ProductBatchesController(WarehouseDbContext context)
        {
            _context = context;
        }

        // KIỂM TRA LÔ HÀNG ĐÃ CÓ LỊCH SỬ GIAO DỊCH CHƯA
        private async Task<bool> HasReceiptHistory(int batchId)
        {
            bool hasImport = await _context.ImportReceipts
                .AnyAsync(r => r.ProductBatchId == batchId);

            bool hasExport = await _context.ExportReceipts
                .AnyAsync(r => r.ProductBatchId == batchId);

            return hasImport || hasExport;
        }

        // NẠP DANH SÁCH SẢN PHẨM VÀ Ô CHỨA
        private async Task LoadDropdowns(
            int? productId = null,
            int? binId = null)
        {
            var products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.ProductId = new SelectList(
                products,
                "Id",
                "Name",
                productId);

            var bins = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .ToListAsync();

            var binOptions = bins.Select(b =>
            {
                var shelfName =
                    b.WarehouseShelf?.Name ?? "Chưa có kệ";

                var areaName =
                    b.WarehouseShelf?.WarehouseArea?.Name
                    ?? "Chưa có khu vực";

                var warehouseName =
                    b.WarehouseShelf?.WarehouseArea?.Warehouse?.Name
                    ?? "Chưa có kho";

                return new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text =
                        $"{b.Code} - {b.Name} | " +
                        $"Kệ: {shelfName} | " +
                        $"Khu vực: {areaName} | " +
                        $"Kho: {warehouseName}"
                };
            }).ToList();

            ViewBag.WarehouseBinId = new SelectList(
                binOptions,
                "Value",
                "Text",
                binId);
        }

        // DANH SÁCH LÔ HÀNG
        public async Task<IActionResult> Index()
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);

            var batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(batches);
        }

        // TRUY XUẤT VÒNG ĐỜI LÔ HÀNG (T67 - BATCH LIFECYCLE HISTORY)
        [HttpGet]
        public async Task<IActionResult> History(int id)
        {
            var batch = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
                return NotFound();

            var imports = await _context.ImportReceipts
                .Include(r => r.Supplier)
                .AsNoTracking()
                .Where(r => r.ProductBatchId == id || r.BatchCode == batch.BatchCode)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var exports = await _context.ExportReceipts
                .AsNoTracking()
                .Where(r => r.ProductBatchId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var transfers = await _context.WarehouseTransfers
                .Include(t => t.SourceBin)
                .Include(t => t.DestinationBin)
                .AsNoTracking()
                .Where(t => t.ProductBatchId == id)
                .OrderByDescending(t => t.TransferDate)
                .ToListAsync();

            var checks = await _context.InventoryCheckDetails
                .Include(d => d.InventoryCheck)
                .AsNoTracking()
                .Where(d => d.ProductBatchId == id)
                .OrderByDescending(d => d.InventoryCheck!.CheckDate)
                .ToListAsync();

            ViewBag.Imports = imports;
            ViewBag.Exports = exports;
            ViewBag.Transfers = transfers;
            ViewBag.Checks = checks;

            return View(batch);
        }

        // GET: THÊM LÔ HÀNG
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View(new ProductBatch
            {
                CreatedAt = DateTime.Now,
                Quantity = 0
            });
        }

        // POST: THÊM LÔ HÀNG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductBatch batch)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == batch.ProductId);

            var bin = await _context.WarehouseBins
                .FirstOrDefaultAsync(b => b.Id == batch.WarehouseBinId);

            if (product == null)
            {
                ModelState.AddModelError(
                    "ProductId",
                    "Vui lòng chọn sản phẩm.");
            }

            if (bin == null)
            {
                ModelState.AddModelError(
                    "WarehouseBinId",
                    "Vui lòng chọn ô chứa.");
            }

            if (await _context.ProductBatches.AnyAsync(b =>
                b.ProductId == batch.ProductId &&
                b.BatchCode == batch.BatchCode &&
                b.WarehouseBinId == batch.WarehouseBinId))
            {
                ModelState.AddModelError(
                    "BatchCode",
                    "Mã lô này đã tồn tại tại ô chứa đã chọn.");
            }

            if (product != null &&
                product.HasExpiryDate &&
                !batch.ExpiryDate.HasValue)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Sản phẩm này bắt buộc phải có hạn sử dụng.");
            }

            if (batch.ManufactureDate.HasValue &&
                batch.ExpiryDate.HasValue &&
                batch.ManufactureDate.Value.Date >
                batch.ExpiryDate.Value.Date)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Hạn sử dụng không được trước ngày sản xuất.");
            }

            if (batch.ExpiryDate.HasValue &&
                batch.ExpiryDate.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Không thể thêm lô hàng đã hết hạn.");
            }

            if (batch.Quantity < 0)
            {
                ModelState.AddModelError(
                    "Quantity",
                    "Số lượng không được nhỏ hơn 0.");
            }

            if (ModelState.IsValid)
            {
                batch.CreatedAt = DateTime.Now;

                _context.ProductBatches.Add(batch);
                await _context.SaveChangesAsync();

                await SyncProductQuantity(batch.ProductId);

                TempData["Success"] =
                    "Thêm lô hàng thành công.";

                return RedirectToAction(nameof(Index));
            }

            await LoadDropdowns(
                batch.ProductId,
                batch.WarehouseBinId);

            return View(batch);
        }

        // GET: SỬA LÔ HÀNG
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var batch = await _context.ProductBatches
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
            {
                return NotFound();
            }

            await LoadDropdowns(
                batch.ProductId,
                batch.WarehouseBinId);

            return View(batch);
        }

        // POST: SỬA LÔ HÀNG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ProductBatch batch)
        {
            if (id != batch.Id)
            {
                return NotFound();
            }

            var existing = await _context.ProductBatches
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            // KHÔNG CHO SỬA LÔ HÀNG ĐÃ CÓ LỊCH SỬ
            if (await HasReceiptHistory(id))
            {
                TempData["Error"] =
                    "Không thể sửa lô hàng vì lô này đã được sử dụng trong phiếu nhập hoặc phiếu xuất.";

                return RedirectToAction(nameof(Index));
            }

            int oldProductId = existing.ProductId;

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == batch.ProductId);

            var bin = await _context.WarehouseBins
                .FirstOrDefaultAsync(b => b.Id == batch.WarehouseBinId);

            if (product == null)
            {
                ModelState.AddModelError(
                    "ProductId",
                    "Vui lòng chọn sản phẩm.");
            }

            if (bin == null)
            {
                ModelState.AddModelError(
                    "WarehouseBinId",
                    "Vui lòng chọn ô chứa.");
            }

            if (await _context.ProductBatches.AnyAsync(b =>
                b.ProductId == batch.ProductId &&
                b.BatchCode == batch.BatchCode &&
                b.WarehouseBinId == batch.WarehouseBinId &&
                b.Id != id))
            {
                ModelState.AddModelError(
                    "BatchCode",
                    "Mã lô này đã tồn tại tại ô chứa đã chọn.");
            }

            if (product != null &&
                product.HasExpiryDate &&
                !batch.ExpiryDate.HasValue)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Sản phẩm này bắt buộc phải có hạn sử dụng.");
            }

            if (batch.ManufactureDate.HasValue &&
                batch.ExpiryDate.HasValue &&
                batch.ManufactureDate.Value.Date >
                batch.ExpiryDate.Value.Date)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Hạn sử dụng không được trước ngày sản xuất.");
            }

            if (batch.ExpiryDate.HasValue &&
                batch.ExpiryDate.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "Không thể cập nhật lô hàng đã hết hạn.");
            }

            if (batch.Quantity < 0)
            {
                ModelState.AddModelError(
                    "Quantity",
                    "Số lượng không được nhỏ hơn 0.");
            }

            if (ModelState.IsValid)
            {
                existing.ProductId = batch.ProductId;
                existing.WarehouseBinId = batch.WarehouseBinId;
                existing.BatchCode = batch.BatchCode;
                existing.ManufactureDate = batch.ManufactureDate;
                existing.ExpiryDate = batch.ExpiryDate;
                existing.Quantity = batch.Quantity;

                await _context.SaveChangesAsync();

                await SyncProductQuantity(oldProductId);

                if (oldProductId != batch.ProductId)
                {
                    await SyncProductQuantity(batch.ProductId);
                }

                TempData["Success"] =
                    "Cập nhật lô hàng thành công.";

                return RedirectToAction(nameof(Index));
            }

            await LoadDropdowns(
                batch.ProductId,
                batch.WarehouseBinId);

            return View(batch);
        }

        // XÓA LÔ HÀNG
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var batch = await _context.ProductBatches
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
            {
                return NotFound();
            }

            // KHÔNG CHO XÓA LÔ HÀNG ĐÃ CÓ LỊCH SỬ
            if (await HasReceiptHistory(id))
            {
                TempData["Error"] =
                    "Không thể xóa lô hàng vì lô này đã được sử dụng trong phiếu nhập hoặc phiếu xuất.";

                return RedirectToAction(nameof(Index));
            }

            int productId = batch.ProductId;

            _context.ProductBatches.Remove(batch);

            await _context.SaveChangesAsync();

            await SyncProductQuantity(productId);

            TempData["Success"] =
                "Đã xóa lô hàng.";

            return RedirectToAction(nameof(Index));
        }

        // ĐỒNG BỘ SỐ LƯỢNG SẢN PHẨM THEO TỔNG CÁC LÔ
        private async Task SyncProductQuantity(int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return;
            }

            product.Quantity = await _context.ProductBatches
                .Where(b => b.ProductId == productId)
                .SumAsync(b => b.Quantity);

            await _context.SaveChangesAsync();
        }
    }
}
