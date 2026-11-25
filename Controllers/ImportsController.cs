
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class ImportsController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly IStockCardService _stockCardService;
        private readonly IWarehousePermissionService _permissionService;

        public ImportsController(
            WarehouseDbContext context,
            IStockCardService stockCardService,
            IWarehousePermissionService permissionService)
        {
            _context = context;
            _stockCardService = stockCardService;
            _permissionService = permissionService;
        }

        // DANH SÁCH VÀ TÌM KIẾM PHIẾU NHẬP
        public async Task<IActionResult> Index(
            string? searchString,
            DateTime? fromDate,
            DateTime? toDate,
            string? status)
        {
            // THỐNG KÊ PHIẾU NHẬP
            var allReceipts = _context.ImportReceipts.AsNoTracking();

            // Nhân viên chỉ xem thống kê phiếu của mình
            if (!User.IsInRole("Admin"))
            {
                var currentUser = User.Identity?.Name;

                allReceipts = allReceipts.Where(x =>
                    x.CreatedBy == currentUser);
            }

            ViewBag.TotalReceipts = await allReceipts.CountAsync();

            ViewBag.PendingReceipts = await allReceipts
                .CountAsync(x => x.Status == ReceiptStatus.Pending);

            ViewBag.ApprovedReceipts = await allReceipts
                .CountAsync(x => x.Status == ReceiptStatus.Approved);

            ViewBag.RejectedReceipts = await allReceipts
                .CountAsync(x => x.Status == ReceiptStatus.Rejected);

            ViewBag.CancelledReceipts = await allReceipts
                .CountAsync(x => x.Status == ReceiptStatus.Cancelled);

            // Tổng số lượng đã được duyệt
            ViewBag.TotalImportedQuantity = await allReceipts
                .Where(x => x.Status == ReceiptStatus.Approved)
                .SumAsync(x => (int?)x.Quantity) ?? 0;

            // DANH SÁCH PHIẾU NHẬP
            var receipts = _context.ImportReceipts
                .Include(x => x.Supplier)
                .Include(x => x.Product)
                .Include(x => x.ProductBatch)
                .Include(x => x.WarehouseBin)
                    .ThenInclude(b => b!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking();

            // Nhân viên chỉ xem phiếu do mình tạo
            if (!User.IsInRole("Admin"))
            {
                var currentUser = User.Identity?.Name;

                receipts = receipts.Where(x =>
                    x.CreatedBy == currentUser);
            }

            // TÌM KIẾM
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();

                receipts = receipts.Where(x =>
                    x.ReceiptCode.Contains(keyword) ||
                    (x.Product != null &&
                     x.Product.Name.Contains(keyword)) ||
                    (x.Supplier != null &&
                     x.Supplier.Name.Contains(keyword)) ||
                    (x.BatchCode != null &&
                     x.BatchCode.Contains(keyword)));
            }

            // LỌC NGÀY
            if (fromDate.HasValue)
            {
                receipts = receipts.Where(x =>
                    x.CreatedAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                receipts = receipts.Where(x =>
                    x.CreatedAt < toDate.Value.Date.AddDays(1));
            }

            // LỌC TRẠNG THÁI
            if (!string.IsNullOrWhiteSpace(status))
            {
                receipts = receipts.Where(x =>
                    x.Status == status);
            }

            ViewBag.SearchString = searchString;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            return View(await receipts
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync());
        }

        // GET: TẠO PHIẾU NHẬP
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);
            await LoadData();

            return View(new ImportReceipt
            {
                BatchCode = await GenerateBatchCodeAsync(),
                UnitPrice = 0m
            });
        }

        // LẤY DANH SÁCH SẢN PHẨM THUỘC NHÀ CUNG CẤP ĐÃ CHỌN (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            var products = await _context.SupplierProducts
                .Include(sp => sp.Product)
                .AsNoTracking()
                .Where(sp => sp.SupplierId == supplierId && sp.Product != null)
                .OrderBy(sp => sp.Product!.Name)
                .Select(sp => new
                {
                    id = sp.Product!.Id,
                    code = sp.Product.Code ?? $"SP-{sp.Product.Id:D4}",
                    name = sp.Product.Name,
                    hasExpiryDate = sp.Product.HasExpiryDate
                })
                .ToListAsync();

            return Json(new { success = true, products });
        }

        // POST: TẠO PHIẾU NHẬP

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ImportReceipt receipt)
        {
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            // Thông tin do hệ thống tự tạo
            receipt.ReceiptCode = "TEMP-" + Guid.NewGuid().ToString("N");
            receipt.CreatedAt = DateTime.Now;
            receipt.Status = ReceiptStatus.Pending;
            receipt.CreatedBy = User.Identity?.Name;
            receipt.ProductBatchId = null;

            // Không sử dụng đơn giá
            receipt.UnitPrice = 0m;

            // Tạo mã lô nếu chưa có
            receipt.BatchCode = string.IsNullOrWhiteSpace(receipt.BatchCode)
                ? await GenerateBatchCodeAsync()
                : receipt.BatchCode.Trim();

            // Bỏ kiểm tra các trường do hệ thống tự tạo
            ModelState.Remove(nameof(receipt.ReceiptCode));
            ModelState.Remove(nameof(receipt.Status));
            ModelState.Remove(nameof(receipt.CreatedBy));
            ModelState.Remove(nameof(receipt.ProductBatchId));
            ModelState.Remove(nameof(receipt.BatchCode));
            ModelState.Remove(nameof(receipt.UnitPrice));

            // Kiểm tra nhà cung cấp
            bool supplierExists = await _context.Suppliers
                .AnyAsync(x => x.Id == receipt.SupplierId);

            if (!supplierExists)
            {
                ModelState.AddModelError(
                    nameof(receipt.SupplierId),
                    "Vui lòng chọn nhà cung cấp hợp lệ.");
            }

            // Kiểm tra sản phẩm
            var product = await _context.Products
                .FirstOrDefaultAsync(x => x.Id == receipt.ProductId);

            if (product == null)
            {
                ModelState.AddModelError(
                    nameof(receipt.ProductId),
                    "Vui lòng chọn sản phẩm hợp lệ.");
            }
            else if (supplierExists)
            {
                // Ràng buộc: Nhà cung cấp chỉ được cung cấp đúng những sản phẩm đã đăng ký cho nhà cung cấp đó
                bool isAllowedForSupplier = await _context.SupplierProducts
                    .AnyAsync(sp => sp.SupplierId == receipt.SupplierId && sp.ProductId == receipt.ProductId);

                if (!isAllowedForSupplier)
                {
                    ModelState.AddModelError(
                        nameof(receipt.ProductId),
                        "Nhà cung cấp đã chọn không cung cấp sản phẩm này! Vui lòng chọn đúng sản phẩm của nhà cung cấp.");
                }
            }

            // Kiểm tra ô chứa và sức chứa tối đa (T39)
            var selectedBin = receipt.WarehouseBinId.HasValue
                ? await _context.WarehouseBins
                    .Include(b => b.ProductBatches)
                    .FirstOrDefaultAsync(x => x.Id == receipt.WarehouseBinId.Value)
                : null;

            if (selectedBin == null)
            {
                ModelState.AddModelError(
                    nameof(receipt.WarehouseBinId),
                    "Vui lòng chọn ô chứa hợp lệ.");
            }
            else if (receipt.Quantity > 0)
            {
                var (allowed, denyReason) = await _permissionService.CheckBinPermissionAsync(User, selectedBin.Id, "Import");
                if (!allowed)
                {
                    ModelState.AddModelError(nameof(receipt.WarehouseBinId), denyReason!);
                }

                int currentUsed = selectedBin.ProductBatches?.Sum(b => b.Quantity) ?? 0;
                int maxCap = selectedBin.MaxCapacity > 0 ? selectedBin.MaxCapacity : 1000;
                if (currentUsed + receipt.Quantity > maxCap)
                {
                    ModelState.AddModelError(
                        nameof(receipt.WarehouseBinId),
                        $"Ô chứa {selectedBin.Code} không đủ chỗ trống! Đang chứa: {currentUsed}/{maxCap}, cần nhập: {receipt.Quantity}. Hãy dùng nút 'Gợi ý ô trống' bên dưới.");
                }
            }

            // Kiểm tra số lượng
            if (receipt.Quantity <= 0)
            {
                ModelState.AddModelError(
                    nameof(receipt.Quantity),
                    "Số lượng nhập phải lớn hơn 0.");
            }

            // Kiểm tra ngày sản xuất và hạn sử dụng
            if (receipt.ManufactureDate.HasValue &&
                receipt.ExpiryDate.HasValue &&
                receipt.ManufactureDate.Value.Date >
                receipt.ExpiryDate.Value.Date)
            {
                ModelState.AddModelError(
                    nameof(receipt.ExpiryDate),
                    "Hạn sử dụng phải sau ngày sản xuất.");
            }

            if (product != null &&
                product.HasExpiryDate &&
                !receipt.ExpiryDate.HasValue)
            {
                ModelState.AddModelError(
                    nameof(receipt.ExpiryDate),
                    "Sản phẩm này bắt buộc phải có hạn sử dụng.");
            }

            if (receipt.ExpiryDate.HasValue &&
                receipt.ExpiryDate.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(receipt.ExpiryDate),
                    "Không thể nhập lô hàng đã hết hạn.");
            }

            // Kiểm tra mã lô trùng
            bool batchExists =
                await _context.ProductBatches.AnyAsync(
                    x => x.BatchCode == receipt.BatchCode) ||
                await _context.ImportReceipts.AnyAsync(
                    x => x.BatchCode == receipt.BatchCode);

            if (batchExists)
            {
                receipt.BatchCode = await GenerateBatchCodeAsync();
            }

            // Nếu dữ liệu không hợp lệ
            if (!ModelState.IsValid)
            {
                await LoadData(
                    receipt.SupplierId,
                    receipt.ProductId,
                    receipt.WarehouseBinId);

                return View(receipt);
            }

            // Lưu phiếu nhập chờ duyệt
            _context.ImportReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            // Tạo mã phiếu chính thức
            receipt.ReceiptCode = "PN" + receipt.Id.ToString("D3");

            // Ghi nhật ký hoạt động
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = "Tạo phiếu nhập chờ duyệt",
                Description =
                    $"Tạo phiếu {receipt.ReceiptCode}, " +
                    $"sản phẩm: {product!.Name}, " +
                    $"mã lô: {receipt.BatchCode}, " +
                    $"số lượng: {receipt.Quantity}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Đã tạo phiếu nhập và gửi quản lý duyệt. Tồn kho chưa thay đổi.";

            return RedirectToAction(nameof(Index));
        }


        // DUYỆT PHIẾU NHẬP
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var receipt = await _context.ImportReceipts
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (receipt == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (receipt.Status != ReceiptStatus.Pending)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Chỉ được duyệt phiếu đang chờ duyệt.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                var product = await _context.Products
                    .FirstOrDefaultAsync(x => x.Id == receipt.ProductId);

                bool supplierExists = await _context.Suppliers
                    .AnyAsync(x => x.Id == receipt.SupplierId);

                bool binExists = receipt.WarehouseBinId.HasValue &&
                    await _context.WarehouseBins.AnyAsync(
                        x => x.Id == receipt.WarehouseBinId.Value);

                if (product == null ||
                    !supplierExists ||
                    !binExists ||
                    string.IsNullOrWhiteSpace(receipt.BatchCode) ||
                    receipt.Quantity <= 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Thông tin phiếu nhập không hợp lệ.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                if (product.HasExpiryDate && !receipt.ExpiryDate.HasValue)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Sản phẩm bắt buộc có hạn sử dụng.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                if (receipt.ManufactureDate.HasValue &&
                    receipt.ExpiryDate.HasValue &&
                    receipt.ManufactureDate.Value.Date >
                    receipt.ExpiryDate.Value.Date)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Ngày sản xuất không được sau hạn sử dụng.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                if (receipt.ExpiryDate.HasValue &&
                    receipt.ExpiryDate.Value.Date < DateTime.Today)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Lô hàng đã hết hạn, không thể duyệt.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                // Kiểm tra mã lô đã được dùng cho sản phẩm hoặc ô chứa khác
                bool conflictingBatch =
                    await _context.ProductBatches.AnyAsync(x =>
                        x.BatchCode == receipt.BatchCode &&
                        (x.ProductId != receipt.ProductId ||
                         x.WarehouseBinId != receipt.WarehouseBinId));

                if (conflictingBatch)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Mã lô đã được sử dụng cho sản phẩm hoặc ô chứa khác.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                // KIỂM TRA SỨC CHỨA TỐI ĐA CỦA Ô CHỨA (T39 - MAX CAPACITY CONTROL)
                var bin = await _context.WarehouseBins.FirstOrDefaultAsync(b => b.Id == receipt.WarehouseBinId);
                if (bin != null)
                {
                    int currentBinStock = await _context.ProductBatches
                        .Where(x => x.WarehouseBinId == bin.Id)
                        .SumAsync(x => x.Quantity);

                    if (currentBinStock + receipt.Quantity > bin.MaxCapacity)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Ô chứa {bin.Code} đã vượt quá sức chứa tối đa! Tồn hiện tại: {currentBinStock}, Sức chứa tối đa: {bin.MaxCapacity}, Cần nhập: {receipt.Quantity}. Vui lòng chọn ô khác hoặc điều chuyển bớt hàng.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                // Tìm lô hàng hiện có
                var batch = await _context.ProductBatches
                    .FirstOrDefaultAsync(x =>
                        x.ProductId == receipt.ProductId &&
                        x.WarehouseBinId == receipt.WarehouseBinId &&
                        x.BatchCode == receipt.BatchCode);

                if (batch != null)
                {
                    if (batch.ManufactureDate?.Date !=
                            receipt.ManufactureDate?.Date ||
                        batch.ExpiryDate?.Date !=
                            receipt.ExpiryDate?.Date)
                    {
                        await transaction.RollbackAsync();

                        TempData["Error"] =
                            "Ngày sản xuất hoặc hạn sử dụng không khớp với lô đã có.";

                        return RedirectToAction(nameof(Details), new { id });
                    }

                    if (batch.Quantity > int.MaxValue - receipt.Quantity)
                    {
                        await transaction.RollbackAsync();

                        TempData["Error"] =
                            "Số lượng sau khi nhập vượt giới hạn.";

                        return RedirectToAction(nameof(Details), new { id });
                    }

                    batch.Quantity += receipt.Quantity;
                }
                else
                {
                    batch = new ProductBatch
                    {
                        ProductId = receipt.ProductId,
                        WarehouseBinId = receipt.WarehouseBinId!.Value,
                        BatchCode = receipt.BatchCode,
                        ManufactureDate = receipt.ManufactureDate,
                        ExpiryDate = receipt.ExpiryDate,
                        Quantity = receipt.Quantity,
                        CreatedAt = DateTime.Now
                    };

                    _context.ProductBatches.Add(batch);
                }

                await _context.SaveChangesAsync();

                receipt.ProductBatchId = batch.Id;
                receipt.Status = ReceiptStatus.Approved;
                receipt.ApprovedBy = User.Identity?.Name;
                receipt.ApprovedAt = DateTime.Now;

                // Cập nhật tồn kho sản phẩm
                product.Quantity = await _context.ProductBatches
                    .Where(x => x.ProductId == product.Id)
                    .SumAsync(x => x.Quantity);

                // GHI NHẬN THẺ KHO (STOCK CARD) - T107
                var supplier = await _context.Suppliers.FindAsync(receipt.SupplierId);
                await _stockCardService.RecordEntryAsync(
                    receiptCode: receipt.ReceiptCode,
                    transactionType: "Nhập kho",
                    productId: receipt.ProductId,
                    productBatchId: batch.Id,
                    warehouseBinId: receipt.WarehouseBinId,
                    quantityChange: receipt.Quantity,
                    unitPrice: receipt.UnitPrice,
                    note: $"Nhập kho từ NCC: {supplier?.Name}. {receipt.Note}".Trim(),
                    createdBy: User.Identity?.Name ?? "Admin",
                    transactionDate: DateTime.Now
                );

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Duyệt phiếu nhập",
                    Description =
                        $"Duyệt phiếu {receipt.ReceiptCode}, " +
                        $"sản phẩm: {product.Name}, " +
                        $"số lượng: {receipt.Quantity}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Duyệt phiếu nhập thành công. Tồn kho đã được cập nhật.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Có lỗi khi duyệt phiếu nhập. Vui lòng kiểm tra dữ liệu.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // TỪ CHỐI PHIẾU
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Vui lòng nhập lý do từ chối.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var receipt = await _context.ImportReceipts
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (receipt == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (receipt.Status != ReceiptStatus.Pending)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Chỉ được từ chối phiếu đang chờ duyệt.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                receipt.Status = ReceiptStatus.Rejected;
                receipt.RejectedBy = User.Identity?.Name;
                receipt.RejectedAt = DateTime.Now;
                receipt.RejectionReason = reason.Trim();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Từ chối phiếu nhập",
                    Description =
                        $"Từ chối phiếu {receipt.ReceiptCode}. " +
                        $"Lý do: {receipt.RejectionReason}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đã từ chối phiếu nhập. Tồn kho không thay đổi.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Không thể từ chối phiếu. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // HỦY PHIẾU
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var receipt = await _context.ImportReceipts
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (receipt == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                bool isAdmin = User.IsInRole("Admin");
                bool isOwner = receipt.CreatedBy == User.Identity?.Name;

                if (receipt.Status != ReceiptStatus.Pending ||
                    (!isAdmin && !isOwner))
                {
                    await transaction.RollbackAsync();
                    return Forbid();
                }

                receipt.Status = ReceiptStatus.Cancelled;
                receipt.CancelledBy = User.Identity?.Name;
                receipt.CancelledAt = DateTime.Now;

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Hủy phiếu nhập",
                    Description =
                        $"Hủy phiếu nhập {receipt.ReceiptCode} khi chưa duyệt.",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đã hủy phiếu nhập. Tồn kho không thay đổi.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Không thể hủy phiếu. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        // CHI TIẾT PHIẾU
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var receipt = await _context.ImportReceipts
                .Include(x => x.Supplier)
                .Include(x => x.Product)
                .Include(x => x.ProductBatch)
                .Include(x => x.WarehouseBin)
                    .ThenInclude(b => b!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (receipt == null)
                return NotFound();

            if (!User.IsInRole("Admin") &&
                receipt.CreatedBy != User.Identity?.Name)
            {
                return Forbid();
            }

            return View(receipt);
        }

        // TẠO MÃ LÔ TỰ ĐỘNG
        private async Task<string> GenerateBatchCodeAsync()
        {
            string code;

            do
            {
                code = $"LOT-{DateTime.Now:yyyyMMdd}-" +
                       Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            }
            while (
                await _context.ProductBatches.AnyAsync(x => x.BatchCode == code) ||
                await _context.ImportReceipts.AnyAsync(x => x.BatchCode == code)
            );

            return code;
        }

        // NẠP DỮ LIỆU DROPDOWN
        private async Task LoadData(
            int? supplierId = null,
            int? productId = null,
            int? warehouseBinId = null)
        {
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.SupplierId = new SelectList(
                suppliers, "Id", "Name", supplierId);

            List<object> productOptions;
            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var supplierProducts = await _context.SupplierProducts
                    .Include(sp => sp.Product)
                    .AsNoTracking()
                    .Where(sp => sp.SupplierId == supplierId.Value && sp.Product != null)
                    .OrderBy(sp => sp.Product!.Name)
                    .Select(sp => new
                    {
                        Id = sp.Product!.Id,
                        Name = $"{(sp.Product.Code ?? $"SP-{sp.Product.Id:D4}")} - {sp.Product.Name}"
                    })
                    .ToListAsync();

                productOptions = supplierProducts.Cast<object>().ToList();
            }
            else
            {
                productOptions = new List<object>();
            }

            ViewBag.ProductId = new SelectList(
                productOptions, "Id", "Name", productId);

            var bins = await _context.WarehouseBins
                .Include(b => b.ProductBatches)
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .ToListAsync();

            var binOptions = bins.Select(b =>
            {
                string warehouseName =
                    b.WarehouseShelf?.WarehouseArea?.Warehouse?.Name
                    ?? "Chưa có kho";

                string areaName =
                    b.WarehouseShelf?.WarehouseArea?.Name
                    ?? "Chưa có khu vực";

                string shelfName =
                    b.WarehouseShelf?.Name
                    ?? "Chưa có kệ";

                int used = b.ProductBatches?.Sum(x => x.Quantity) ?? 0;
                int max = b.MaxCapacity > 0 ? b.MaxCapacity : 1000;
                int free = Math.Max(0, max - used);

                return new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{warehouseName} - {areaName} - {shelfName} - Ô {b.Code} (Trống: {free}/{max})"
                };
            }).ToList();

            ViewBag.WarehouseBinId = new SelectList(
                binOptions, "Value", "Text", warehouseBinId);
        }

        // GỢI Ý VỊ TRÍ Ô KỆ TRỐNG TỐI ƯU (T66 - AUTO LOCATION SUGGEST)
        [HttpGet]
        public async Task<IActionResult> SuggestBinLocation(int? productId, int quantity = 1)
        {
            if (quantity <= 0) quantity = 1;

            var bins = await _context.WarehouseBins
                .Include(b => b.ProductBatches)
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .Where(b => b.WarehouseShelf != null &&
                            b.WarehouseShelf.WarehouseArea != null &&
                            b.WarehouseShelf.WarehouseArea.Warehouse != null &&
                            b.WarehouseShelf.WarehouseArea.Warehouse.IsActive)
                .ToListAsync();

            var candidates = bins
                .Select(b =>
                {
                    int used = b.ProductBatches?.Sum(x => x.Quantity) ?? 0;
                    int max = b.MaxCapacity > 0 ? b.MaxCapacity : 1000;
                    int free = Math.Max(0, max - used);
                    bool hasSameProduct = productId.HasValue &&
                        (b.ProductBatches?.Any(x => x.ProductId == productId.Value && x.Quantity > 0) ?? false);
                    bool isEmpty = used == 0;

                    return new
                    {
                        binId = b.Id,
                        binCode = b.Code,
                        binName = b.Name,
                        floor = b.FloorNumber,
                        location = $"{b.WarehouseShelf!.WarehouseArea!.Warehouse!.Name} › {b.WarehouseShelf.WarehouseArea.Name} › {b.WarehouseShelf.Name} › Ô {b.Code}",
                        used,
                        maxCapacity = max,
                        freeSpace = free,
                        fillPercent = (int)Math.Min(100, Math.Round((double)used / max * 100)),
                        hasSameProduct,
                        isEmpty,
                        reason = hasSameProduct
                            ? "Gom chung khu vực với lô cùng sản phẩm"
                            : (isEmpty ? "Ô đang trống hoàn toàn" : "Còn dư nhiều sức chứa")
                    };
                })
                .Where(x => x.freeSpace >= quantity)
                .OrderByDescending(x => x.hasSameProduct)
                .ThenByDescending(x => x.isEmpty)
                .ThenByDescending(x => x.freeSpace)
                .Take(4)
                .ToList();

            return Json(new { success = true, suggestions = candidates });
        }
    }
}
