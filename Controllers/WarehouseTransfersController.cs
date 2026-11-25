
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;
using System.Data;
using System.Security.Claims;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class WarehouseTransfersController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly IStockCardService _stockCardService;

        public WarehouseTransfersController(
            WarehouseDbContext context,
            IStockCardService stockCardService)
        {
            _context = context;
            _stockCardService = stockCardService;
        }

        // DANH SÁCH PHIẾU ĐIỀU CHUYỂN
        public async Task<IActionResult> Index()
        {
            var transfers = await _context.WarehouseTransfers
                .Include(t => t.ProductBatch)
                    .ThenInclude(b => b!.Product)
                .Include(t => t.SourceBin)
                .Include(t => t.DestinationBin)
                .OrderByDescending(t => t.TransferDate)
                .ToListAsync();

            return View(transfers);
        }

        // FORM TẠO PHIẾU
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadFormData();

            return View(new WarehouseTransfer
            {
                TransferDate = DateTime.Now
            });
        }

        // XỬ LÝ ĐIỀU CHUYỂN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseTransfer model)
        {
            ModelState.Remove(nameof(model.TransferCode));
            ModelState.Remove(nameof(model.CreatedBy));
            ModelState.Remove(nameof(model.ProductBatch));
            ModelState.Remove(nameof(model.SourceBin));
            ModelState.Remove(nameof(model.DestinationBin));

            if (model.SourceBinId <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.SourceBinId),
                    "Vui lòng chọn ô kho nguồn.");
            }

            if (model.DestinationBinId <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.DestinationBinId),
                    "Vui lòng chọn ô kho đích.");
            }

            if (model.SourceBinId == model.DestinationBinId)
            {
                ModelState.AddModelError(
                    nameof(model.DestinationBinId),
                    "Ô nguồn và ô đích không được trùng nhau.");
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.Quantity),
                    "Số lượng điều chuyển phải lớn hơn 0.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormData(model.ProductBatchId);
                return View(model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                // Lấy lô hàng nguồn
                var batch = await _context.ProductBatches
                    .FirstOrDefaultAsync(b => b.Id == model.ProductBatchId);

                // Kiểm tra ô kho đích
                var destinationBin = await _context.WarehouseBins
                    .FirstOrDefaultAsync(b => b.Id == model.DestinationBinId);

                if (batch == null || destinationBin == null)
                {
                    ModelState.AddModelError(
                        "",
                        "Không tìm thấy lô hàng hoặc ô kho đích.");

                    await transaction.RollbackAsync();
                    await LoadFormData(model.ProductBatchId);
                    return View(model);
                }

                // Kiểm tra vị trí nguồn
                if (batch.WarehouseBinId != model.SourceBinId)
                {
                    ModelState.AddModelError(
                        "",
                        "Ô nguồn không khớp với vị trí hiện tại của lô hàng.");

                    await transaction.RollbackAsync();
                    await LoadFormData(model.ProductBatchId);
                    return View(model);
                }

                // Kiểm tra tồn kho
                if (model.Quantity > batch.Quantity)
                {
                    ModelState.AddModelError(
                        nameof(model.Quantity),
                        $"Số lượng tồn tại ô nguồn chỉ còn {batch.Quantity}.");

                    await transaction.RollbackAsync();
                    await LoadFormData(model.ProductBatchId);
                    return View(model);
                }

                // KIỂM TRA SỨC CHỨA TỐI ĐA Ô ĐÍCH (T39 - MAX CAPACITY CONTROL)
                int currentDestStock = await _context.ProductBatches
                    .Where(b => b.WarehouseBinId == destinationBin.Id)
                    .SumAsync(b => b.Quantity);

                if (currentDestStock + model.Quantity > destinationBin.MaxCapacity)
                {
                    ModelState.AddModelError(
                        nameof(model.DestinationBinId),
                        $"Ô đích {destinationBin.Code} không đủ sức chứa! Hiện có: {currentDestStock}, Sức chứa tối đa: {destinationBin.MaxCapacity}, Cần chuyển: {model.Quantity}.");

                    await transaction.RollbackAsync();
                    await LoadFormData(model.ProductBatchId);
                    return View(model);
                }

                // Trừ số lượng tại ô nguồn
                batch.Quantity -= model.Quantity;

                // Tìm lô hàng giống nhau tại ô đích
                var destinationBatch = await _context.ProductBatches
                    .FirstOrDefaultAsync(b =>
                        b.ProductId == batch.ProductId &&
                        b.BatchCode == batch.BatchCode &&
                        b.WarehouseBinId == model.DestinationBinId &&
                        b.ManufactureDate == batch.ManufactureDate &&
                        b.ExpiryDate == batch.ExpiryDate);

                if (destinationBatch != null)
                {
                    // Nếu đã có lô tương ứng thì cộng số lượng
                    destinationBatch.Quantity += model.Quantity;
                }
                else
                {
                    // Nếu chưa có thì tạo lô tại ô đích
                    destinationBatch = new ProductBatch
                    {
                        ProductId = batch.ProductId,
                        WarehouseBinId = model.DestinationBinId,
                        BatchCode = batch.BatchCode,
                        ManufactureDate = batch.ManufactureDate,
                        ExpiryDate = batch.ExpiryDate,
                        Quantity = model.Quantity,
                        CreatedAt = DateTime.Now
                    };

                    _context.ProductBatches.Add(destinationBatch);
                }

                // Tạo mã phiếu điều chuyển
                var transferCode =
                    "DC" + Guid.NewGuid().ToString("N")[..12].ToUpper();

                var transferDate = model.TransferDate == default
                    ? DateTime.Now
                    : model.TransferDate;

                var userName = User.Identity?.Name
                    ?? User.FindFirstValue(ClaimTypes.Name)
                    ?? "System";

                // Lưu phiếu điều chuyển
                var transfer = new WarehouseTransfer
                {
                    TransferCode = transferCode,
                    TransferDate = transferDate,
                    ProductBatchId = batch.Id,
                    SourceBinId = model.SourceBinId,
                    DestinationBinId = model.DestinationBinId,
                    Quantity = model.Quantity,
                    Note = model.Note,
                    CreatedBy = userName
                };

                _context.WarehouseTransfers.Add(transfer);

                // Ghi nhật ký hoạt động
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = userName,
                    Action = "Điều chuyển kho",
                    Description =
                        $"Phiếu {transferCode}: Lô {batch.BatchCode}, " +
                        $"chuyển {model.Quantity} sản phẩm từ ô " +
                        $"{model.SourceBinId} sang ô {model.DestinationBinId}.",
                    CreatedAt = DateTime.Now
                });

                // GHI NHẬN THẺ KHO (STOCK CARD) - T107
                // 1. Thẻ kho xuất chuyển đi từ ô nguồn
                await _stockCardService.RecordEntryAsync(
                    receiptCode: transferCode,
                    transactionType: "Điều chuyển đi",
                    productId: batch.ProductId,
                    productBatchId: batch.Id,
                    warehouseBinId: model.SourceBinId,
                    quantityChange: -model.Quantity,
                    note: $"Chuyển sang ô {destinationBin.Code}. {model.Note}".Trim(),
                    createdBy: userName,
                    transactionDate: transferDate
                );

                // 2. Thẻ kho nhập chuyển đến ô đích
                await _stockCardService.RecordEntryAsync(
                    receiptCode: transferCode,
                    transactionType: "Điều chuyển đến",
                    productId: batch.ProductId,
                    productBatchId: destinationBatch.Id,
                    warehouseBinId: model.DestinationBinId,
                    quantityChange: model.Quantity,
                    note: $"Nhận chuyển từ ô {model.SourceBinId}. {model.Note}".Trim(),
                    createdBy: userName,
                    transactionDate: transferDate
                );

                // Lưu dữ liệu
                // Không thay đổi Product.Quantity vì chỉ chuyển vị trí
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Điều chuyển thành công! Mã phiếu: {transferCode}";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Có lỗi xảy ra khi điều chuyển. Vui lòng kiểm tra lại dữ liệu.");

                await LoadFormData(model.ProductBatchId);
                return View(model);
            }
        }

        // CHI TIẾT PHIẾU ĐIỀU CHUYỂN
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transfer = await _context.WarehouseTransfers
                .Include(t => t.ProductBatch)
                    .ThenInclude(b => b!.Product)
                .Include(t => t.SourceBin)
                .Include(t => t.DestinationBin)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null)
            {
                return NotFound();
            }

            return View(transfer);
        }

        // DỮ LIỆU CHO FORM
        private async Task LoadFormData(int? selectedBatchId = null)
        {
            var batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                .Where(b => b.Quantity > 0)
                .OrderBy(b => b.Product!.Name)
                .ToListAsync();

            ViewBag.ProductBatches = new SelectList(
                batches.Select(b => new
                {
                    b.Id,
                    Display = b.Product!.Name +
                              " - Lô " + b.BatchCode +
                              " - Tồn: " + b.Quantity +
                              " - Ô: " + b.WarehouseBin!.Code
                }),
                "Id",
                "Display",
                selectedBatchId);

            var bins = await _context.WarehouseBins
                .OrderBy(b => b.Code)
                .ToListAsync();

            ViewBag.Bins = new SelectList(
                bins,
                "Id",
                "Name");
        }
    }
}
