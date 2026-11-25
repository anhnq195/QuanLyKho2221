using System.Data;
using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class InventoryChecksController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly IStockCardService _stockCardService;

        public InventoryChecksController(
            WarehouseDbContext context,
            IStockCardService stockCardService)
        {
            _context = context;
            _stockCardService = stockCardService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? keyword)
        {
            var query = _context.InventoryChecks
                .AsNoTracking()
                .Include(x => x.Details)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x => x.CheckCode.Contains(keyword) || x.CreatedBy.Contains(keyword));
            }

            ViewBag.Keyword = keyword;
            return View(await query.OrderByDescending(x => x.CheckDate).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = await BuildCreateViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InventoryCheckCreateViewModel model)
        {
            if (model.Items == null || model.Items.Count == 0)
            {
                ModelState.AddModelError("", "Không có lô hàng hoặc sản phẩm để kiểm kê.");
                return View(await BuildCreateViewModel(model.Note));
            }

            if (model.Items.Any(x => x.ProductId <= 0 || x.ActualQuantity < 0))
                ModelState.AddModelError("", "Dữ liệu kiểm kê không hợp lệ.");

            var batchIds = model.Items.Where(x => x.ProductBatchId.HasValue)
                .Select(x => x.ProductBatchId!.Value).ToList();

            if (batchIds.Distinct().Count() != batchIds.Count)
                ModelState.AddModelError("", "Một lô hàng đang xuất hiện nhiều lần trong phiếu.");

            if (!ModelState.IsValid)
                return View(await BuildCreateViewModel(model.Note, model.Items));

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var batches = await _context.ProductBatches
                    .Include(b => b.Product)
                    .Where(b => batchIds.Contains(b.Id))
                    .ToListAsync();

                if (batches.Count != batchIds.Count)
                {
                    ModelState.AddModelError("", "Có lô hàng không còn tồn tại. Vui lòng tải lại trang.");
                    await transaction.RollbackAsync();
                    return View(await BuildCreateViewModel(model.Note));
                }

                var batchById = batches.ToDictionary(b => b.Id);
                var check = new InventoryCheck
                {
                    CheckCode = "KK" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                    CheckDate = DateTime.Now,
                    Note = model.Note,
                    CreatedBy = User.Identity?.Name ?? "Unknown",
                    IsConfirmed = false
                };

                foreach (var item in model.Items)
                {
                    Product product;
                    int systemQuantity;

                    if (item.ProductBatchId.HasValue)
                    {
                        var batch = batchById[item.ProductBatchId.Value];
                        if (batch.ProductId != item.ProductId)
                        {
                            ModelState.AddModelError("", "Lô hàng không khớp với sản phẩm.");
                            await transaction.RollbackAsync();
                            return View(await BuildCreateViewModel(model.Note));
                        }

                        product = batch.Product!;
                        systemQuantity = batch.Quantity;
                    }
                    else
                    {
                        product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId)
                            ?? throw new InvalidOperationException("Sản phẩm không còn tồn tại.");

                        bool hasBatch = await _context.ProductBatches.AnyAsync(b => b.ProductId == product.Id);
                        if (hasBatch)
                        {
                            ModelState.AddModelError("", "Sản phẩm đã có lô hàng. Vui lòng kiểm kê theo từng lô.");
                            await transaction.RollbackAsync();
                            return View(await BuildCreateViewModel(model.Note));
                        }
                        systemQuantity = product.Quantity;
                    }

                    check.Details.Add(new InventoryCheckDetail
                    {
                        ProductId = product.Id,
                        ProductBatchId = item.ProductBatchId,
                        SystemQuantity = systemQuantity,
                        ActualQuantity = item.ActualQuantity
                    });
                }

                _context.InventoryChecks.Add(check);
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Unknown",
                    Action = "Tạo phiếu kiểm kê",
                    Description = $"Tạo phiếu kiểm kê {check.CheckCode} theo lô hàng/vị trí",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Tạo phiếu kiểm kê thành công!";
                return RedirectToAction(nameof(Details), new { id = check.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var errorMessage = ex.InnerException?.Message ?? ex.Message;

                ModelState.AddModelError("",
                    "Lỗi khi lưu phiếu kiểm kê: " + errorMessage);

                return View(await BuildCreateViewModel(model.Note, model.Items));
            }
        }

        private async Task<InventoryCheckCreateViewModel> BuildCreateViewModel(
            string? note = null,
            List<InventoryCheckItemViewModel>? postedItems = null)
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);

            var batches = await _context.ProductBatches
                .AsNoTracking()
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(shelf => shelf!.WarehouseArea)
                            .ThenInclude(area => area!.Warehouse)
                .OrderBy(b => b.Product!.Name)
                .ThenBy(b => b.BatchCode)
                .ToListAsync();

            var items = new List<InventoryCheckItemViewModel>();

            foreach (var batch in batches)
            {
                var posted = postedItems?.FirstOrDefault(x => x.ProductBatchId == batch.Id);
                var bin = batch.WarehouseBin;
                var shelf = bin?.WarehouseShelf;
                var area = shelf?.WarehouseArea;
                var warehouse = area?.Warehouse;
                string location = string.Join(" / ", new[]
                {
                    warehouse?.Name,
                    area?.Name,
                    shelf?.Name,
                    bin?.Name
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                items.Add(new InventoryCheckItemViewModel
                {
                    ProductId = batch.ProductId,
                    ProductBatchId = batch.Id,
                    ProductName = batch.Product?.Name ?? "Sản phẩm",
                    ProductCode = batch.Product?.Code,
                    BatchCode = batch.BatchCode,
                    BinLocation = string.IsNullOrWhiteSpace(location) ? "Chưa xác định vị trí" : location,
                    SystemQuantity = batch.Quantity,
                    ActualQuantity = posted?.ActualQuantity ?? batch.Quantity
                });
            }

            // Sản phẩm chưa có lô được hiển thị riêng để không bỏ sót tồn cũ.
            var productsWithoutBatches = await _context.Products
                .AsNoTracking()
                .Where(p => !_context.ProductBatches.Any(b => b.ProductId == p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync();

            foreach (var product in productsWithoutBatches)
            {
                var posted = postedItems?.FirstOrDefault(x => x.ProductId == product.Id && !x.ProductBatchId.HasValue);
                items.Add(new InventoryCheckItemViewModel
                {
                    ProductId = product.Id,
                    ProductBatchId = null,
                    ProductName = product.Name,
                    ProductCode = product.Code,
                    BatchCode = "Chưa phân lô",
                    BinLocation = "Chưa xác định vị trí",
                    SystemQuantity = product.Quantity,
                    ActualQuantity = posted?.ActualQuantity ?? product.Quantity
                });
            }

            return new InventoryCheckCreateViewModel { Note = note, Items = items };
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var check = await _context.InventoryChecks
                .AsNoTracking()
                .Include(x => x.Details)
                    .ThenInclude(d => d.Product)
                .Include(x => x.Details)
                    .ThenInclude(d => d.ProductBatch)
                        .ThenInclude(b => b!.WarehouseBin)
                            .ThenInclude(bin => bin!.WarehouseShelf)
                                .ThenInclude(shelf => shelf!.WarehouseArea)
                                    .ThenInclude(area => area!.Warehouse)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (check == null) return NotFound();
            return View(check);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Confirm(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var check = await _context.InventoryChecks
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (check == null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            if (check.IsConfirmed)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Phiếu này đã được xác nhận trước đó.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!check.Details.Any())
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Phiếu kiểm kê không có dữ liệu.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var batchDetails = check.Details.Where(d => d.ProductBatchId.HasValue).ToList();
            var legacyDetails = check.Details.Where(d => !d.ProductBatchId.HasValue).ToList();
            var batchIds = batchDetails.Select(d => d.ProductBatchId!.Value).ToList();
            var batches = await _context.ProductBatches.Where(b => batchIds.Contains(b.Id)).ToListAsync();

            if (batches.Count != batchIds.Distinct().Count())
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Có lô hàng không còn tồn tại. Không thể xác nhận phiếu.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Phiếu cũ chỉ kiểm kê theo sản phẩm không thể tự phân bổ chênh lệch vào nhiều lô.
            foreach (var detail in legacyDetails)
            {
                bool productHasBatches = await _context.ProductBatches.AnyAsync(b => b.ProductId == detail.ProductId);
                if (productHasBatches)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Phiếu này được tạo theo cách kiểm kê cũ, không có thông tin lô hàng. Vui lòng tạo phiếu kiểm kê mới theo từng lô.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var batchDictionary = batches.ToDictionary(b => b.Id);
            foreach (var detail in batchDetails)
                batchDictionary[detail.ProductBatchId!.Value].Quantity = detail.ActualQuantity;

            var affectedProductIds = check.Details.Select(d => d.ProductId).Distinct().ToList();
            var products = await _context.Products.Where(p => affectedProductIds.Contains(p.Id)).ToListAsync();

            foreach (var product in products)
            {
                var productBatches = await _context.ProductBatches
                    .Where(b => b.ProductId == product.Id)
                    .ToListAsync();

                if (productBatches.Count > 0)
                {
                    long total = productBatches.Sum(b => (long)b.Quantity);
                    if (total > int.MaxValue)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Tổng tồn của sản phẩm {product.Name} vượt giới hạn cho phép.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    product.Quantity = (int)total;
                }
                else
                {
                    var legacyDetail = legacyDetails.FirstOrDefault(d => d.ProductId == product.Id);
                    if (legacyDetail != null)
                        product.Quantity = legacyDetail.ActualQuantity;
                }
            }

            // GHI NHẬN THẺ KHO ĐỐI VỚI CÁC MỤC CÓ CHÊNH LỆCH KIỂM KÊ (T107)
            foreach (var detail in check.Details)
            {
                if (detail.Difference != 0)
                {
                    int? binId = detail.ProductBatchId.HasValue && batchDictionary.ContainsKey(detail.ProductBatchId.Value)
                        ? batchDictionary[detail.ProductBatchId.Value].WarehouseBinId
                        : null;

                    await _stockCardService.RecordEntryAsync(
                        receiptCode: check.CheckCode,
                        transactionType: "Cân bằng kiểm kê",
                        productId: detail.ProductId,
                        productBatchId: detail.ProductBatchId,
                        warehouseBinId: binId,
                        quantityChange: detail.Difference,
                        note: $"Cân bằng kiểm kê {check.CheckCode}: Thực tế {detail.ActualQuantity} so với Sổ sách {detail.SystemQuantity} (Chênh lệch: {(detail.Difference > 0 ? "+" : "")}{detail.Difference})",
                        createdBy: User.Identity?.Name ?? "Admin",
                        transactionDate: check.CheckDate
                    );
                }
            }

            check.IsConfirmed = true;
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Unknown",
                Action = "Xác nhận kiểm kê",
                Description = $"Xác nhận phiếu {check.CheckCode}, cập nhật tồn kho theo lô và vị trí",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Xác nhận kiểm kê thành công! Số lượng từng lô và tồn tổng sản phẩm đã được đồng bộ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // XUẤT BIÊN BẢN KIỂM KÊ & ĐỐI SOÁT KHO RA EXCEL (T32 - EXPORT STOCKTAKE RESULT)
        [HttpGet]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var check = await _context.InventoryChecks
                .AsNoTracking()
                .Include(x => x.Details)
                    .ThenInclude(d => d.Product)
                .Include(x => x.Details)
                    .ThenInclude(d => d.ProductBatch)
                        .ThenInclude(b => b!.WarehouseBin)
                            .ThenInclude(bin => bin!.WarehouseShelf)
                                .ThenInclude(shelf => shelf!.WarehouseArea)
                                    .ThenInclude(area => area!.Warehouse)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (check == null)
                return NotFound();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BienBanKiemKe");

            ws.Cell(1, 1).Value = $"BIÊN BẢN KIỂM KÊ & ĐỐI SOÁT CHÊNH LỆCH KHO ({check.CheckCode})";
            ws.Range(1, 1, 1, 8).Merge();
            var title = ws.Range(1, 1, 1, 8);
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 30;

            ws.Cell(2, 1).Value = $"Ngày kiểm kê: {check.CheckDate:dd/MM/yyyy HH:mm} | Người lập: {check.CreatedBy} | Trạng thái: {(check.IsConfirmed ? "Đã xác nhận" : "Chờ xác nhận")}";
            ws.Range(2, 1, 2, 8).Merge();

            string[] headers = { "STT", "Mã SKU", "Tên sản phẩm", "Mã lô hàng", "Vị trí lưu kho", "Tồn sổ sách", "Tồn thực tế", "Chênh lệch" };
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(4, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int r = 5;
            int idx = 1;
            foreach (var d in check.Details)
            {
                var bin = d.ProductBatch?.WarehouseBin;
                var shelf = bin?.WarehouseShelf;
                var area = shelf?.WarehouseArea;
                var wh = area?.Warehouse;
                string loc = wh != null ? $"{wh.Name} > {area?.Name} > {shelf?.Name} > Ô {bin?.Code}" : "Chưa phân lô";

                ws.Cell(r, 1).Value = idx++;
                ws.Cell(r, 2).Value = d.Product?.Code ?? "N/A";
                ws.Cell(r, 3).Value = d.Product?.Name ?? "";
                ws.Cell(r, 4).Value = d.ProductBatch?.BatchCode ?? "Chưa phân lô";
                ws.Cell(r, 5).Value = loc;
                ws.Cell(r, 6).Value = d.SystemQuantity;
                ws.Cell(r, 7).Value = d.ActualQuantity;
                ws.Cell(r, 8).Value = d.Difference;
                ws.Range(r, 1, r, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BienBanKiemKe_{check.CheckCode}.xlsx");
        }
    }
}
