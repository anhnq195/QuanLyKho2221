
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
    public class ExportsController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly IWarehouseRuleService _ruleService;
        private readonly IStockCardService _stockCardService;
        private readonly IWarehousePermissionService _permissionService;

        public ExportsController(
            WarehouseDbContext context,
            IWarehouseRuleService ruleService,
            IStockCardService stockCardService,
            IWarehousePermissionService permissionService)
        {
            _context = context;
            _ruleService = ruleService;
            _stockCardService = stockCardService;
            _permissionService = permissionService;
        }

        // DANH SÁCH LẤY HÀNG TỐI ƯU LỘ TRÌNH KHO (T106 - AUTO PICKING LIST)
        [HttpGet]
        public async Task<IActionResult> PickingList(string? ids)
        {
            var query = _context.ExportReceipts
                .Include(x => x.Product)
                .Include(x => x.ProductBatch)
                    .ThenInclude(b => b!.WarehouseBin)
                        .ThenInclude(bin => bin!.WarehouseShelf)
                            .ThenInclude(s => s!.WarehouseArea)
                                .ThenInclude(a => a!.Warehouse)
                .AsNoTracking();

            if (!User.IsInRole("Admin"))
            {
                var currentUser = User.Identity?.Name;
                query = query.Where(x => x.CreatedBy == currentUser);
            }

            if (!string.IsNullOrWhiteSpace(ids))
            {
                var parsedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();
                query = query.Where(x => parsedIds.Contains(x.Id));
            }
            else
            {
                // Mặc định lấy các phiếu đang chờ duyệt (Pending) cần soạn hàng
                query = query.Where(x => x.Status == ReceiptStatus.Pending);
            }

            var receipts = await query.ToListAsync();

            // Sắp xếp tối ưu lộ trình di chuyển trong kho:
            // Kho (Code) -> Khu vực (Code) -> Kệ (Code) -> Tầng (1 -> N) -> Ô (Code) -> HSD (FEFO)
            var sortedStops = receipts
                .OrderBy(r => r.ProductBatch?.WarehouseBin?.WarehouseShelf?.WarehouseArea?.Warehouse?.Code ?? "ZZZ")
                .ThenBy(r => r.ProductBatch?.WarehouseBin?.WarehouseShelf?.WarehouseArea?.Code ?? "ZZZ")
                .ThenBy(r => r.ProductBatch?.WarehouseBin?.WarehouseShelf?.Code ?? "ZZZ")
                .ThenBy(r => r.ProductBatch?.WarehouseBin?.FloorNumber ?? 999)
                .ThenBy(r => r.ProductBatch?.WarehouseBin?.Code ?? "ZZZ")
                .ThenBy(r => r.ProductBatch?.ExpiryDate ?? DateTime.MaxValue)
                .ToList();

            return View(sortedStops);
        }

        // DANH SÁCH PHIẾU XUẤT
        public async Task<IActionResult> Index(
            string? searchString,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var receipts = _context.ExportReceipts
                .Include(x => x.Product)
                .Include(x => x.ProductBatch)
                    .ThenInclude(b => b!.WarehouseBin)
                        .ThenInclude(bin => bin!.WarehouseShelf)
                            .ThenInclude(s => s!.WarehouseArea)
                                .ThenInclude(a => a!.Warehouse)
                .AsNoTracking();

            // EMPLOYEE CHỈ XEM PHIẾU DO MÌNH TẠO
            if (!User.IsInRole("Admin"))
            {
                var currentUser = User.Identity?.Name;

                receipts = receipts.Where(x =>
                    x.CreatedBy == currentUser);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();

                receipts = receipts.Where(x =>
                    x.ReceiptCode.Contains(keyword) ||
                    (x.Product != null &&
                     x.Product.Name.Contains(keyword)) ||
                    (x.Receiver != null &&
                     x.Receiver.Contains(keyword)));
            }

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

            ViewBag.SearchString = searchString;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(await receipts
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync());
        }

        // TẠO PHIẾU XUẤT - GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadBatches();
            await LoadProducts();

            return View(new ExportReceipt());
        }

        // XEM TRƯỚC PHÂN BỔ LÔ THEO QUY TẮC (FEFO / FIFO / LIFO) - AJAX
        [HttpGet]
        public async Task<IActionResult> PreviewRuleAllocation(int productId, int quantity, string rule = "FEFO")
        {
            if (productId <= 0 || quantity <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn sản phẩm và số lượng xuất hợp lệ." });
            }

            var plan = await _ruleService.CalculateOutboundAllocationAsync(productId, quantity, rule);
            return Json(new { success = true, plan });
        }

        // TẠO PHIẾU XUẤT TỰ ĐỘNG THEO QUY TẮC (FEFO / FIFO / LIFO)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAuto(AutoExportRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập vào chưa hợp lệ.";
                await LoadBatches();
                await LoadProducts(model.ProductId);
                return View("Create", new ExportReceipt());
            }

            var plan = await _ruleService.CalculateOutboundAllocationAsync(model.ProductId, model.Quantity, model.Rule);

            if (!plan.IsSufficient)
            {
                TempData["Error"] = $"Không đủ số lượng khả dụng để xuất theo quy tắc {plan.Rule}! " +
                                    $"Cần xuất: {model.Quantity}, nhưng chỉ tìm thấy {plan.TotalAllocatedQuantity} sản phẩm khả dụng trong kho.";
                await LoadBatches();
                await LoadProducts(model.ProductId);
                return View("Create", new ExportReceipt());
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var createdCodes = new List<string>();

                foreach (var alloc in plan.Allocations)
                {
                    var receipt = new ExportReceipt
                    {
                        ReceiptCode = "TEMP-" + Guid.NewGuid().ToString("N"),
                        ProductId = model.ProductId,
                        ProductBatchId = alloc.BatchId,
                        Quantity = alloc.AllocatedQuantity,
                        UnitPrice = 0,
                        Receiver = model.Receiver,
                        Note = $"[Tự động {plan.Rule}] Lô: {alloc.BatchCode}. {model.Note}".Trim(),
                        CreatedAt = DateTime.Now,
                        Status = ReceiptStatus.Pending,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.ExportReceipts.Add(receipt);
                    await _context.SaveChangesAsync();

                    receipt.ReceiptCode = "PX" + receipt.Id.ToString("D3");
                    createdCodes.Add(receipt.ReceiptCode);

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        UserName = User.Identity?.Name ?? "Không xác định",
                        Action = $"Tạo phiếu xuất tự động ({plan.Rule})",
                        Description = $"Tạo phiếu {receipt.ReceiptCode}, sản phẩm {plan.ProductName}, lô {alloc.BatchCode}, số lượng: {alloc.AllocatedQuantity}",
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                TempData["Success"] = $"Đã tạo thành công {createdCodes.Count} phiếu xuất ({string.Join(", ", createdCodes)}) " +
                                      $"theo quy tắc {plan.Rule} với tổng số lượng {model.Quantity}. Hàng đã được giữ chỗ và đang chờ quản lý duyệt.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Có lỗi xảy ra khi tạo phiếu xuất tự động: " + ex.Message;
                await LoadBatches();
                await LoadProducts(model.ProductId);
                return View("Create", new ExportReceipt());
            }
        }

        // TẠO PHIẾU XUẤT THỦ CÔNG - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExportReceipt receipt)
        {
            receipt.ReceiptCode =
                "TEMP-" + Guid.NewGuid().ToString("N");

            receipt.CreatedAt = DateTime.Now;
            receipt.UnitPrice = 0;
            receipt.Status = ReceiptStatus.Pending;
            receipt.CreatedBy = User.Identity?.Name;

            ModelState.Remove(nameof(receipt.ReceiptCode));
            ModelState.Remove(nameof(receipt.UnitPrice));
            ModelState.Remove(nameof(receipt.ProductId));
            ModelState.Remove(nameof(receipt.Status));
            ModelState.Remove(nameof(receipt.CreatedBy));

            if (!receipt.ProductBatchId.HasValue ||
                receipt.ProductBatchId.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(receipt.ProductBatchId),
                    "Vui lòng chọn lô hàng cần xuất.");
            }

            if (receipt.Quantity <= 0)
            {
                ModelState.AddModelError(
                    nameof(receipt.Quantity),
                    "Số lượng xuất phải lớn hơn 0.");
            }

            if (!ModelState.IsValid)
            {
                await LoadBatches(receipt.ProductBatchId);
                return View(receipt);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var batch = await _context.ProductBatches
                    .Include(x => x.Product)
                    .Include(x => x.WarehouseBin)
                    .FirstOrDefaultAsync(
                        x => x.Id == receipt.ProductBatchId!.Value);

                if (batch == null || batch.Product == null)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        nameof(receipt.ProductBatchId),
                        "Lô hàng không tồn tại.");

                    await LoadBatches(receipt.ProductBatchId);
                    await LoadProducts();
                    return View(receipt);
                }

                // Kiểm tra phân quyền kho (T61)
                var (allowed, denyReason) = await _permissionService.CheckBinPermissionAsync(User, batch.WarehouseBinId, "Export");
                if (!allowed)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(nameof(receipt.ProductBatchId), denyReason!);
                    await LoadBatches(receipt.ProductBatchId);
                    await LoadProducts();
                    return View(receipt);
                }

                if (batch.Quantity <= 0)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        nameof(receipt.ProductBatchId),
                        "Lô hàng hiện không còn tồn kho.");

                    await LoadBatches(receipt.ProductBatchId);
                    return View(receipt);
                }

                // KIỂM TRA LỊCH SỬ NHẬP CỦA LÔ
                var hasImportHistory =
                    await _context.ImportReceipts
                        .AnyAsync(x =>
                            x.ProductBatchId == batch.Id);

                if (hasImportHistory)
                {
                    var hasApprovedImport =
                        await _context.ImportReceipts.AnyAsync(x =>
                            x.ProductBatchId == batch.Id &&
                            x.Status == ReceiptStatus.Approved);

                    if (!hasApprovedImport)
                    {
                        await transaction.RollbackAsync();

                        ModelState.AddModelError(
                            nameof(receipt.ProductBatchId),
                            "Lô hàng chưa có phiếu nhập được duyệt.");

                        await LoadBatches(receipt.ProductBatchId);
                        return View(receipt);
                    }
                }

                // TÍNH SỐ LƯỢNG ĐANG CHỜ XUẤT
                long pendingQuantity =
                    await _context.ExportReceipts
                        .Where(x =>
                            x.ProductBatchId == batch.Id &&
                            x.Status == ReceiptStatus.Pending)
                        .Select(x => (long)x.Quantity)
                        .SumAsync();

                // TÍNH TỒN KHO CÓ THỂ XUẤT
                long availableQuantity =
                    (long)batch.Quantity - pendingQuantity;

                if (availableQuantity < receipt.Quantity)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        nameof(receipt.Quantity),
                        $"Lô {batch.BatchCode} có {batch.Quantity} sản phẩm, " +
                        $"đã có {pendingQuantity} sản phẩm được giữ chỗ " +
                        $"trong các phiếu chờ duyệt. " +
                        $"Số lượng còn có thể xuất: " +
                        $"{Math.Max(0, availableQuantity)}.");

                    await LoadBatches(receipt.ProductBatchId);
                    return View(receipt);
                }

                // GÁN SẢN PHẨM THEO LÔ HÀNG
                receipt.ProductId = batch.ProductId;

                _context.ExportReceipts.Add(receipt);

                await _context.SaveChangesAsync();

                receipt.ReceiptCode =
                    "PX" + receipt.Id.ToString("D3");

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Tạo phiếu xuất chờ duyệt",
                    Description =
                        $"Tạo phiếu xuất {receipt.ReceiptCode}, " +
                        $"sản phẩm: {batch.Product.Name}, " +
                        $"mã lô: {batch.BatchCode}, " +
                        $"số lượng: {receipt.Quantity}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đã tạo phiếu xuất và gửi quản lý duyệt. " +
                    "Số lượng đã được giữ chỗ, tồn kho thực tế " +
                    "chỉ thay đổi khi phiếu được duyệt.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Có lỗi khi tạo phiếu xuất. Vui lòng thử lại.";

                return RedirectToAction(nameof(Create));
            }
        }

        // DUYỆT PHIẾU XUẤT - ADMIN
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
                var receipt = await _context.ExportReceipts
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

                if (!receipt.ProductBatchId.HasValue)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Phiếu xuất chưa có lô hàng.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                var batch = await _context.ProductBatches
                    .Include(x => x.Product)
                    .Include(x => x.WarehouseBin)
                    .FirstOrDefaultAsync(
                        x => x.Id == receipt.ProductBatchId.Value);

                if (batch == null || batch.Product == null)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Không tìm thấy lô hàng của phiếu xuất.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                if (receipt.ProductId != batch.ProductId)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Sản phẩm trong phiếu không khớp với lô hàng.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                // KIỂM TRA LỊCH SỬ NHẬP
                var hasImportHistory =
                    await _context.ImportReceipts.AnyAsync(x =>
                        x.ProductBatchId == batch.Id);

                if (hasImportHistory)
                {
                    var hasApprovedImport =
                        await _context.ImportReceipts.AnyAsync(x =>
                            x.ProductBatchId == batch.Id &&
                            x.Status == ReceiptStatus.Approved);

                    if (!hasApprovedImport)
                    {
                        await transaction.RollbackAsync();

                        TempData["Error"] =
                            "Lô hàng chưa có phiếu nhập được duyệt.";

                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                if (receipt.Quantity <= 0)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Số lượng xuất không hợp lệ.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                // TÍNH SỐ LƯỢNG ĐANG CHỜ XUẤT CỦA CÁC PHIẾU KHÁC
                long otherPendingQuantity =
                    await _context.ExportReceipts
                        .Where(x =>
                            x.ProductBatchId == batch.Id &&
                            x.Status == ReceiptStatus.Pending &&
                            x.Id != receipt.Id)
                        .Select(x => (long)x.Quantity)
                        .SumAsync();

                // KIỂM TRA TỒN KHO SAU KHI TÍNH CẢ CÁC PHIẾU ĐANG CHỜ
                if ((long)batch.Quantity <
                    (long)receipt.Quantity + otherPendingQuantity)
                {
                    await transaction.RollbackAsync();

                    TempData["Error"] =
                        "Không đủ tồn kho để duyệt phiếu này sau khi tính " +
                        "các phiếu xuất đang chờ duyệt.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                // TRỪ TỒN KHO LÔ HÀNG
                batch.Quantity -= receipt.Quantity;

                await _context.SaveChangesAsync();

                // CẬP NHẬT TỔNG TỒN KHO SẢN PHẨM
                batch.Product.Quantity =
                    await _context.ProductBatches
                        .Where(x => x.ProductId == batch.ProductId)
                        .SumAsync(x => x.Quantity);

                // GHI NHẬN THẺ KHO (STOCK CARD) - T107
                await _stockCardService.RecordEntryAsync(
                    receiptCode: receipt.ReceiptCode,
                    transactionType: "Xuất kho",
                    productId: batch.ProductId,
                    productBatchId: batch.Id,
                    warehouseBinId: batch.WarehouseBinId,
                    quantityChange: -receipt.Quantity,
                    unitPrice: receipt.UnitPrice,
                    note: $"Xuất kho cho: {receipt.Receiver}. {receipt.Note}".Trim(),
                    createdBy: User.Identity?.Name ?? "Admin",
                    transactionDate: DateTime.Now
                );

                receipt.Status = ReceiptStatus.Approved;
                receipt.ApprovedBy = User.Identity?.Name;
                receipt.ApprovedAt = DateTime.Now;

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Duyệt phiếu xuất",
                    Description =
                        $"Duyệt phiếu {receipt.ReceiptCode}, " +
                        $"sản phẩm: {batch.Product.Name}, " +
                        $"mã lô: {batch.BatchCode}, " +
                        $"số lượng: {receipt.Quantity}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Duyệt phiếu xuất thành công. Tồn kho đã được trừ.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Có lỗi khi duyệt phiếu xuất. " +
                    "Vui lòng tải lại trang và kiểm tra tồn kho.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // TỪ CHỐI PHIẾU XUẤT - ADMIN
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] =
                    "Vui lòng nhập lý do từ chối phiếu.";

                return RedirectToAction(nameof(Details), new { id });
            }

            reason = reason.Trim();

            if (reason.Length > 500)
            {
                TempData["Error"] =
                    "Lý do từ chối không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var receipt = await _context.ExportReceipts
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
                receipt.RejectionReason = reason;

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Không xác định",
                    Action = "Từ chối phiếu xuất",
                    Description =
                        $"Từ chối phiếu {receipt.ReceiptCode}. " +
                        $"Lý do: {receipt.RejectionReason}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đã từ chối phiếu xuất. Tồn kho không thay đổi.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Không thể từ chối phiếu. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // HỦY PHIẾU XUẤT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var receipt = await _context.ExportReceipts
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (receipt == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                var isAdmin = User.IsInRole("Admin");

                if (receipt.Status != ReceiptStatus.Pending ||
                    (!isAdmin &&
                     receipt.CreatedBy != User.Identity?.Name))
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
                    Action = "Hủy phiếu xuất",
                    Description =
                        $"Hủy phiếu xuất {receipt.ReceiptCode} khi chưa duyệt.",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] =
                    "Đã hủy phiếu xuất. Số lượng giữ chỗ đã được giải phóng.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] =
                    "Không thể hủy phiếu. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        // CHI TIẾT PHIẾU XUẤT
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receipt = await _context.ExportReceipts
                .Include(x => x.Product)
                .Include(x => x.ProductBatch)
                    .ThenInclude(b => b!.WarehouseBin)
                        .ThenInclude(bin => bin!.WarehouseShelf)
                            .ThenInclude(s => s!.WarehouseArea)
                                .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (receipt == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin") &&
                receipt.CreatedBy != User.Identity?.Name)
            {
                return Forbid();
            }

            return View(receipt);
        }

        // NẠP DANH SÁCH LÔ HÀNG CÒN TỒN
        private async Task LoadBatches(
            int? selectedBatchId = null)
        {
            var batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .Where(b => b.Quantity > 0 && b.Product != null)
                .AsNoTracking()
                .OrderBy(b => b.Product!.Name)
                .ToListAsync();

            // LẤY LỊCH SỬ NHẬP CỦA CÁC LÔ
            var importHistory = await _context.ImportReceipts
                .AsNoTracking()
                .Where(x => x.ProductBatchId.HasValue)
                .GroupBy(x => x.ProductBatchId!.Value)
                .Select(g => new
                {
                    BatchId = g.Key,
                    HasApprovedImport = g.Any(
                        x => x.Status == ReceiptStatus.Approved)
                })
                .ToDictionaryAsync(
                    x => x.BatchId,
                    x => x.HasApprovedImport);

            // LẤY SỐ LƯỢNG ĐANG CHỜ XUẤT
            var pendingExports = await _context.ExportReceipts
                .AsNoTracking()
                .Where(x =>
                    x.Status == ReceiptStatus.Pending &&
                    x.ProductBatchId.HasValue)
                .GroupBy(x => x.ProductBatchId!.Value)
                .Select(g => new
                {
                    BatchId = g.Key,
                    Quantity = g.Sum(x => (long)x.Quantity)
                })
                .ToDictionaryAsync(
                    x => x.BatchId,
                    x => x.Quantity);

            var validBatches = batches
                .Where(batch =>
                {
                    if (importHistory.TryGetValue(
                        batch.Id, out var hasApprovedImport))
                    {
                        if (!hasApprovedImport)
                        {
                            return false;
                        }
                    }

                    // LÔ CŨ KHÔNG CÓ LỊCH SỬ NHẬP VẪN ĐƯỢC GIỮ
                    pendingExports.TryGetValue(
                        batch.Id, out var pendingQuantity);

                    long available =
                        (long)batch.Quantity - pendingQuantity;

                    return available > 0;
                })
                .ToList();

            var options = validBatches.Select(b =>
            {
                var productName =
                    b.Product?.Name ?? "Không rõ sản phẩm";

                var binCode =
                    b.WarehouseBin?.Code ?? "Chưa có ô";

                var shelfName =
                    b.WarehouseBin?.WarehouseShelf?.Name
                    ?? "Chưa có kệ";

                var areaName =
                    b.WarehouseBin?.WarehouseShelf?
                        .WarehouseArea?.Name
                    ?? "Chưa có khu vực";

                var warehouseName =
                    b.WarehouseBin?.WarehouseShelf?
                        .WarehouseArea?.Warehouse?.Name
                    ?? "Chưa có kho";

                pendingExports.TryGetValue(
                    b.Id, out var pendingQuantity);

                long available =
                    (long)b.Quantity - pendingQuantity;

                return new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text =
                        $"{productName} | " +
                        $"Lô: {b.BatchCode} | " +
                        $"Kho: {warehouseName} | " +
                        $"Khu: {areaName} | " +
                        $"Kệ: {shelfName} | " +
                        $"Ô: {binCode} | " +
                        $"Tồn: {b.Quantity} | " +
                        $"Chờ xuất: {pendingQuantity} | " +
                        $"Có thể xuất: {available}"
                };
            }).ToList();

            ViewBag.ProductBatchId = new SelectList(
                options,
                "Value",
                "Text",
                selectedBatchId);
        }

        // NẠP DANH SÁCH SẢN PHẨM CÒN TỒN CHO TAB XUẤT TỰ ĐỘNG
        private async Task LoadProducts(int? selectedProductId = null)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.Quantity > 0)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    DisplayName = $"{p.Name} (Mã: {p.Code ?? "N/A"}) - Tồn: {p.Quantity}" + (p.HasExpiryDate ? " [HSD - Ưu tiên FEFO]" : "")
                })
                .ToListAsync();

            ViewBag.ProductId = new SelectList(
                products,
                "Id",
                "DisplayName",
                selectedProductId);
        }
    }
}
