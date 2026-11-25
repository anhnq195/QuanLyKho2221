using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize]
    public class StockCardsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public StockCardsController(WarehouseDbContext context)
        {
            _context = context;
        }

        // XEM THẺ KHO (SỔ CHI TIẾT VẬT TƯ) - T107
        public async Task<IActionResult> Index(
            int? productId,
            DateTime? fromDate,
            DateTime? toDate,
            int? warehouseId)
        {
            // Danh sách sản phẩm để chọn
            var products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = new SelectList(
                products.Select(p => new
                {
                    p.Id,
                    DisplayText = $"{p.Name} (Mã: {p.Code ?? "N/A"}) - Tồn: {p.Quantity}"
                }),
                "Id",
                "DisplayText",
                productId);

            // Danh sách kho để lọc
            var warehouses = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .ToListAsync();

            ViewBag.Warehouses = new SelectList(warehouses, "Id", "Name", warehouseId);

            // Mặc định chọn sản phẩm đầu tiên nếu chưa chọn
            if (!productId.HasValue && products.Any())
            {
                productId = products.First().Id;
            }

            // Khoảng thời gian mặc định: 30 ngày qua
            var effectiveToDate = toDate ?? DateTime.Today;
            var effectiveFromDate = fromDate ?? effectiveToDate.AddDays(-30);

            ViewBag.SelectedProductId = productId;
            ViewBag.FromDate = effectiveFromDate.ToString("yyyy-MM-dd");
            ViewBag.ToDate = effectiveToDate.ToString("yyyy-MM-dd");
            ViewBag.WarehouseId = warehouseId;

            if (!productId.HasValue)
            {
                return View(new StockCardReportViewModel());
            }

            var selectedProduct = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId.Value);

            if (selectedProduct == null)
            {
                return NotFound();
            }

            // Lấy các phát sinh thẻ kho
            var query = _context.StockCards
                .Include(s => s.ProductBatch)
                .Include(s => s.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(shelf => shelf!.WarehouseArea)
                            .ThenInclude(area => area!.Warehouse)
                .Where(s => s.ProductId == productId.Value)
                .AsNoTracking()
                .AsQueryable();

            if (warehouseId.HasValue)
            {
                query = query.Where(s =>
                    s.WarehouseBin != null &&
                    s.WarehouseBin.WarehouseShelf != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea.WarehouseId == warehouseId.Value);
            }

            // Tính tồn đầu kỳ (tất cả các biến động trước effectiveFromDate)
            var priorQuery = _context.StockCards
                .Where(s => s.ProductId == productId.Value && s.TransactionDate < effectiveFromDate.Date);

            if (warehouseId.HasValue)
            {
                priorQuery = priorQuery.Where(s =>
                    s.WarehouseBin != null &&
                    s.WarehouseBin.WarehouseShelf != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea.WarehouseId == warehouseId.Value);
            }

            int openingStock = await priorQuery.SumAsync(s => (int?)s.QuantityChange) ?? 0;

            // Lấy các phát sinh trong kỳ
            var endDate = effectiveToDate.Date.AddDays(1);
            var entries = await query
                .Where(s => s.TransactionDate >= effectiveFromDate.Date && s.TransactionDate < endDate)
                .OrderBy(s => s.TransactionDate)
                .ThenBy(s => s.Id)
                .ToListAsync();

            // Tính toán tồn lũy kế cho từng dòng phát sinh
            int runningBalance = openingStock;
            var reportEntries = new List<StockCardRowViewModel>();

            int totalIn = 0;
            int totalOut = 0;

            foreach (var item in entries)
            {
                int qtyIn = item.QuantityChange > 0 ? item.QuantityChange : 0;
                int qtyOut = item.QuantityChange < 0 ? Math.Abs(item.QuantityChange) : 0;

                totalIn += qtyIn;
                totalOut += qtyOut;
                runningBalance += item.QuantityChange;

                var bin = item.WarehouseBin;
                var shelf = bin?.WarehouseShelf;
                var area = shelf?.WarehouseArea;
                var wh = area?.Warehouse;

                string loc = (wh != null) ? $"{wh.Code}-{shelf?.Code}-{bin?.Code}" : "N/A";

                reportEntries.Add(new StockCardRowViewModel
                {
                    Id = item.Id,
                    TransactionDate = item.TransactionDate,
                    ReceiptCode = item.ReceiptCode,
                    TransactionType = item.TransactionType,
                    BatchCode = item.ProductBatch?.BatchCode ?? "N/A",
                    Location = loc,
                    QuantityIn = qtyIn,
                    QuantityOut = qtyOut,
                    RunningBalance = runningBalance,
                    UnitPrice = item.UnitPrice,
                    Note = item.Note,
                    CreatedBy = item.CreatedBy
                });
            }

            var model = new StockCardReportViewModel
            {
                Product = selectedProduct,
                FromDate = effectiveFromDate,
                ToDate = effectiveToDate,
                OpeningStock = openingStock,
                TotalIn = totalIn,
                TotalOut = totalOut,
                ClosingStock = runningBalance,
                Entries = reportEntries
            };

            return View(model);
        }

        // XUẤT THẺ KHO RA EXCEL (ClosedXML)
        public async Task<IActionResult> ExportExcel(
            int productId,
            DateTime? fromDate,
            DateTime? toDate,
            int? warehouseId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return NotFound();

            var effectiveToDate = toDate ?? DateTime.Today;
            var effectiveFromDate = fromDate ?? effectiveToDate.AddDays(-30);

            var query = _context.StockCards
                .Include(s => s.ProductBatch)
                .Include(s => s.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(shelf => shelf!.WarehouseArea)
                            .ThenInclude(area => area!.Warehouse)
                .Where(s => s.ProductId == productId)
                .AsNoTracking()
                .AsQueryable();

            if (warehouseId.HasValue)
            {
                query = query.Where(s =>
                    s.WarehouseBin != null &&
                    s.WarehouseBin.WarehouseShelf != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea.WarehouseId == warehouseId.Value);
            }

            // Tồn đầu kỳ (áp dụng cùng bộ lọc kho như màn hình web)
            var priorQuery = _context.StockCards
                .Where(s => s.ProductId == productId && s.TransactionDate < effectiveFromDate.Date);

            if (warehouseId.HasValue)
            {
                priorQuery = priorQuery.Where(s =>
                    s.WarehouseBin != null &&
                    s.WarehouseBin.WarehouseShelf != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea != null &&
                    s.WarehouseBin.WarehouseShelf.WarehouseArea.WarehouseId == warehouseId.Value);
            }

            int openingStock = await priorQuery.SumAsync(s => (int?)s.QuantityChange) ?? 0;

            var endDate = effectiveToDate.Date.AddDays(1);
            var entries = await query
                .Where(s => s.TransactionDate >= effectiveFromDate.Date && s.TransactionDate < endDate)
                .OrderBy(s => s.TransactionDate)
                .ThenBy(s => s.Id)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("TheKho");

            // TIÊU ĐỀ
            ws.Cell(1, 1).Value = "THẺ KHO (SỔ CHI TIẾT VẬT TƯ, HÀNG HÓA)";
            ws.Range(1, 1, 1, 8).Merge();
            var title = ws.Range(1, 1, 1, 8);
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(1).Height = 35;

            // THÔNG TIN
            ws.Cell(2, 1).Value = $"Tên hàng hóa: {product.Name} (Mã SKU: {product.Code ?? "N/A"})";
            ws.Range(2, 1, 2, 4).Merge();
            ws.Cell(2, 5).Value = $"Kỳ báo cáo: Từ {effectiveFromDate:dd/MM/yyyy} đến {effectiveToDate:dd/MM/yyyy}";
            ws.Range(2, 5, 2, 8).Merge();

            ws.Cell(3, 1).Value = $"Đơn vị tính / Nhóm: {product.Category?.Name ?? "Chưa phân nhóm"}";
            ws.Range(3, 1, 3, 4).Merge();
            ws.Cell(3, 5).Value = $"Ngày xuất file: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Range(3, 5, 3, 8).Merge();

            ws.Range(2, 1, 3, 8).Style.Font.Italic = true;

            // HEADER BẢNG
            string[] headers = { "Ngày giao dịch", "Số chứng từ", "Loại nghiệp vụ / Diễn giải", "Lô hàng", "Vị trí ô", "Nhập", "Xuất", "Tồn lũy kế" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // DÒNG TỒN ĐẦU KỲ
            ws.Cell(6, 1).Value = effectiveFromDate.ToString("dd/MM/yyyy");
            ws.Cell(6, 2).Value = "-";
            ws.Cell(6, 3).Value = "--- SỐ DƯ ĐẦU KỲ ---";
            ws.Cell(6, 3).Style.Font.Bold = true;
            ws.Cell(6, 4).Value = "-";
            ws.Cell(6, 5).Value = "-";
            ws.Cell(6, 6).Value = 0;
            ws.Cell(6, 7).Value = 0;
            ws.Cell(6, 8).Value = openingStock;
            ws.Cell(6, 8).Style.Font.Bold = true;
            ws.Range(6, 1, 6, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(6, 1, 6, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#fef3c7");

            int row = 7;
            int balance = openingStock;
            int sumIn = 0;
            int sumOut = 0;

            foreach (var item in entries)
            {
                int qIn = item.QuantityChange > 0 ? item.QuantityChange : 0;
                int qOut = item.QuantityChange < 0 ? Math.Abs(item.QuantityChange) : 0;
                sumIn += qIn;
                sumOut += qOut;
                balance += item.QuantityChange;

                ws.Cell(row, 1).Value = item.TransactionDate.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 2).Value = item.ReceiptCode;
                ws.Cell(row, 3).Value = $"{item.TransactionType} - {item.Note}";
                ws.Cell(row, 4).Value = item.ProductBatch?.BatchCode ?? "-";
                ws.Cell(row, 5).Value = item.WarehouseBin?.Code ?? "-";
                ws.Cell(row, 6).Value = qIn;
                ws.Cell(row, 7).Value = qOut;
                ws.Cell(row, 8).Value = balance;

                for (int col = 1; col <= 8; col++)
                {
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                row++;
            }

            // DÒNG TỔNG CỘNG
            ws.Cell(row, 3).Value = "TỔNG PHÁT SINH TRONG KỲ";
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = sumIn;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = sumOut;
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 8).Value = balance;
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Range(row, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");

            ws.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            string fileName = $"TheKho_{product.Code ?? product.Id.ToString()}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }

    public class StockCardReportViewModel
    {
        public Product? Product { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int OpeningStock { get; set; }
        public int TotalIn { get; set; }
        public int TotalOut { get; set; }
        public int ClosingStock { get; set; }
        public List<StockCardRowViewModel> Entries { get; set; } = new();
    }

    public class StockCardRowViewModel
    {
        public int Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string BatchCode { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int QuantityIn { get; set; }
        public int QuantityOut { get; set; }
        public int RunningBalance { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Note { get; set; }
        public string? CreatedBy { get; set; }
    }
}

