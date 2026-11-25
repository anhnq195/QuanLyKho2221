using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    /// <summary>
    /// T55  (RQ55 / PB08)  - Tính tỷ lệ luân chuyển hàng tồn kho (Turnover Rate) & tỷ lệ lấp đầy
    /// T97  (RQ97 / PB08)  - Tải biểu đồ biến động tồn kho & sức chứa
    /// T98  (RQ98 / PB08)  - Phân tích xu hướng nhập xuất & hàng tồn chậm luân chuyển (Fast / Slow / Dead Stock)
    /// T108 (RQ108 / PB10) - Phân tích hiệu suất vận hành và tỷ lệ lấp đầy từng kho
    /// </summary>
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public AnalyticsController(WarehouseDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int days = 90)
        {
            if (days != 30 && days != 90 && days != 180 && days != 365)
                days = 90;

            var model = await BuildAnalyticsAsync(days);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int days = 90)
        {
            var data = await BuildAnalyticsAsync(days);

            using var wb = new XLWorkbook();

            // SHEET 1: TỶ LỆ LUÂN CHUYỂN HÀNG TỒN KHO (TURNOVER & FSN)
            var ws1 = wb.Worksheets.Add("LuanChuyenTonKho");
            ws1.Cell(1, 1).Value = $"BÁO CÁO TỶ LỆ LUÂN CHUYỂN & PHÂN TÍCH HÀNG TỒN KHO ({days} NGÀY QUA)";
            ws1.Range(1, 1, 1, 9).Merge();
            var title1 = ws1.Range(1, 1, 1, 9);
            title1.Style.Font.Bold = true;
            title1.Style.Font.FontSize = 14;
            title1.Style.Font.FontColor = XLColor.White;
            title1.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            title1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Row(1).Height = 30;

            string[] headers1 =
            {
                "Mã SKU", "Tên sản phẩm", "Danh mục",
                "Nhập trong kỳ", "Xuất trong kỳ", "Tồn hiện tại",
                "Giá trị tồn (VNĐ)", "Vòng quay (Turnover)", "Phân nhóm tốc độ"
            };
            for (int i = 0; i < headers1.Length; i++)
            {
                var c = ws1.Cell(3, i + 1);
                c.Value = headers1[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int r = 4;
            foreach (var item in data.ProductTurnovers)
            {
                ws1.Cell(r, 1).Value = item.Code ?? "N/A";
                ws1.Cell(r, 2).Value = item.ProductName;
                ws1.Cell(r, 3).Value = item.CategoryName;
                ws1.Cell(r, 4).Value = item.ImportedInPeriod;
                ws1.Cell(r, 5).Value = item.ExportedInPeriod;
                ws1.Cell(r, 6).Value = item.CurrentStock;
                ws1.Cell(r, 7).Value = item.InventoryValue;
                ws1.Cell(r, 7).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r, 8).Value = item.TurnoverRate;
                ws1.Cell(r, 8).Style.NumberFormat.Format = "0.00";
                ws1.Cell(r, 9).Value = item.VelocityLabel;
                ws1.Range(r, 1, r, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r++;
            }
            ws1.Columns().AdjustToContents();

            // SHEET 2: HIỆU SUẤT LẤP ĐẦY KHO
            var ws2 = wb.Worksheets.Add("HieuSuatKho");
            ws2.Cell(1, 1).Value = "BÁO CÁO SỨC CHỨA & TỶ LỆ LẤP ĐẦY CÁC KHO";
            ws2.Range(1, 1, 1, 7).Merge();
            var title2 = ws2.Range(1, 1, 1, 7);
            title2.Style.Font.Bold = true;
            title2.Style.Font.FontSize = 14;
            title2.Style.Font.FontColor = XLColor.White;
            title2.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f766e");
            title2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws2.Row(1).Height = 30;

            string[] headers2 = { "Mã kho", "Tên kho", "Tổng số ô", "Ô đang chứa hàng", "Đang chứa (SL)", "Sức chứa tối đa", "Tỷ lệ lấp đầy (%)" };
            for (int i = 0; i < headers2.Length; i++)
            {
                var c = ws2.Cell(3, i + 1);
                c.Value = headers2[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int r2 = 4;
            foreach (var w in data.WarehouseOccupancies)
            {
                ws2.Cell(r2, 1).Value = w.WarehouseCode;
                ws2.Cell(r2, 2).Value = w.WarehouseName;
                ws2.Cell(r2, 3).Value = w.TotalBins;
                ws2.Cell(r2, 4).Value = w.OccupiedBins;
                ws2.Cell(r2, 5).Value = w.UsedQuantity;
                ws2.Cell(r2, 6).Value = w.MaxCapacity;
                ws2.Cell(r2, 7).Value = w.OccupancyPercent;
                ws2.Range(r2, 1, r2, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r2++;
            }
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PhanTichKho_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        private async Task<WarehouseAnalyticsViewModel> BuildAnalyticsAsync(int days)
        {
            var fromDate = DateTime.Today.AddDays(-days);

            // 1. Dữ liệu sức chứa từng kho & ô kệ (T55, T108)
            var warehouses = await _context.Warehouses
                .Include(w => w.Areas)
                    .ThenInclude(a => a.Shelves)
                        .ThenInclude(s => s.Bins)
                            .ThenInclude(b => b.ProductBatches)
                .AsNoTracking()
                .OrderBy(w => w.Code)
                .ToListAsync();

            var occupancies = new List<WarehouseOccupancyItem>();

            foreach (var w in warehouses)
            {
                var bins = w.Areas
                    .SelectMany(a => a.Shelves)
                    .SelectMany(s => s.Bins)
                    .ToList();

                int totalBins = bins.Count;
                int occupiedBins = bins.Count(b => (b.ProductBatches?.Sum(x => x.Quantity) ?? 0) > 0);
                int usedQty = bins.Sum(b => b.ProductBatches?.Sum(x => x.Quantity) ?? 0);
                int maxCap = bins.Sum(b => b.MaxCapacity > 0 ? b.MaxCapacity : 1000);
                double pct = maxCap > 0 ? Math.Round(usedQty * 100.0 / maxCap, 1) : 0;

                occupancies.Add(new WarehouseOccupancyItem
                {
                    WarehouseId = w.Id,
                    WarehouseCode = w.Code,
                    WarehouseName = w.Name,
                    TotalAreas = w.Areas.Count,
                    TotalBins = totalBins,
                    OccupiedBins = occupiedBins,
                    UsedQuantity = usedQty,
                    MaxCapacity = maxCap,
                    OccupancyPercent = pct
                });
            }

            // 2. Dữ liệu nhập - xuất trong kỳ để tính Turnover Rate (T55, T98)
            var importsByProduct = await _context.ImportReceipts
                .AsNoTracking()
                .Where(x => x.Status == ReceiptStatus.Approved && x.CreatedAt >= fromDate)
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Qty);

            var exportsByProduct = await _context.ExportReceipts
                .AsNoTracking()
                .Where(x => x.Status == ReceiptStatus.Approved && x.CreatedAt >= fromDate)
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Qty = g.Sum(x => x.Quantity),
                    LastExportDate = g.Max(x => (DateTime?)x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.ProductId, x => (x.Qty, x.LastExportDate));

            var products = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            var turnovers = new List<ProductTurnoverItem>();

            foreach (var p in products)
            {
                importsByProduct.TryGetValue(p.Id, out int imported);
                exportsByProduct.TryGetValue(p.Id, out var expInfo);
                int exported = expInfo.Qty;

                // Tồn đầu kỳ ước tính = Tồn cuối - Nhập + Xuất
                int estimatedOpening = Math.Max(0, p.Quantity - imported + exported);
                double avgInventory = (estimatedOpening + p.Quantity) / 2.0;
                if (avgInventory <= 0 && exported > 0)
                    avgInventory = exported;

                double turnoverRate = avgInventory > 0
                    ? Math.Round(exported / avgInventory, 2)
                    : 0;

                double? dsi = turnoverRate > 0
                    ? Math.Round(days / turnoverRate, 1)
                    : null;

                string velocityCode;
                string velocityLabel;
                if (exported == 0 && p.Quantity > 0)
                {
                    velocityCode = "DEAD";
                    velocityLabel = "Tồn đọng (Không xuất)";
                }
                else if (turnoverRate >= 1.0 || exported >= 20)
                {
                    velocityCode = "FAST";
                    velocityLabel = "Luân chuyển nhanh";
                }
                else if (exported > 0)
                {
                    velocityCode = "SLOW";
                    velocityLabel = "Luân chuyển chậm";
                }
                else
                {
                    velocityCode = "EMPTY";
                    velocityLabel = "Không tồn";
                }

                turnovers.Add(new ProductTurnoverItem
                {
                    ProductId = p.Id,
                    Code = p.Code,
                    ProductName = p.Name,
                    CategoryName = p.Category?.Name ?? "Chưa phân nhóm",
                    ImportedInPeriod = imported,
                    ExportedInPeriod = exported,
                    CurrentStock = p.Quantity,
                    UnitPrice = p.Price,
                    InventoryValue = p.Quantity * p.Price,
                    AverageStock = Math.Round(avgInventory, 1),
                    TurnoverRate = turnoverRate,
                    DaysSalesOfInventory = dsi,
                    LastExportDate = expInfo.LastExportDate,
                    VelocityCode = velocityCode,
                    VelocityLabel = velocityLabel
                });
            }

            turnovers = turnovers
                .OrderByDescending(x => x.TurnoverRate)
                .ThenByDescending(x => x.ExportedInPeriod)
                .ToList();

            int totalUsed = occupancies.Sum(x => x.UsedQuantity);
            int totalMax = occupancies.Sum(x => x.MaxCapacity);
            double overallOccupancy = totalMax > 0 ? Math.Round(totalUsed * 100.0 / totalMax, 1) : 0;

            int totalExportedAll = turnovers.Sum(x => x.ExportedInPeriod);
            double totalAvgStock = turnovers.Sum(x => x.AverageStock);
            double overallTurnover = totalAvgStock > 0
                ? Math.Round(totalExportedAll / totalAvgStock, 2)
                : 0;

            int fastCount = turnovers.Count(x => x.VelocityCode == "FAST");
            int slowCount = turnovers.Count(x => x.VelocityCode == "SLOW");
            int deadCount = turnovers.Count(x => x.VelocityCode == "DEAD");

            return new WarehouseAnalyticsViewModel
            {
                PeriodDays = days,
                OverallOccupancyPercent = overallOccupancy,
                TotalUsedCapacity = totalUsed,
                TotalMaxCapacity = totalMax,
                OverallTurnoverRate = overallTurnover,
                TotalExportedInPeriod = totalExportedAll,
                TotalImportedInPeriod = turnovers.Sum(x => x.ImportedInPeriod),
                FastMovingCount = fastCount,
                SlowMovingCount = slowCount,
                DeadStockCount = deadCount,
                WarehouseOccupancies = occupancies,
                ProductTurnovers = turnovers,
                WarehouseNamesJson = JsonSerializer.Serialize(occupancies.Select(x => x.WarehouseName)),
                WarehouseUsedJson = JsonSerializer.Serialize(occupancies.Select(x => x.UsedQuantity)),
                WarehouseFreeJson = JsonSerializer.Serialize(occupancies.Select(x => Math.Max(0, x.MaxCapacity - x.UsedQuantity))),
                VelocityCountsJson = JsonSerializer.Serialize(new[] { fastCount, slowCount, deadCount })
            };
        }
    }

    public class WarehouseAnalyticsViewModel
    {
        public int PeriodDays { get; set; }
        public double OverallOccupancyPercent { get; set; }
        public int TotalUsedCapacity { get; set; }
        public int TotalMaxCapacity { get; set; }
        public double OverallTurnoverRate { get; set; }
        public int TotalImportedInPeriod { get; set; }
        public int TotalExportedInPeriod { get; set; }
        public int FastMovingCount { get; set; }
        public int SlowMovingCount { get; set; }
        public int DeadStockCount { get; set; }

        public List<WarehouseOccupancyItem> WarehouseOccupancies { get; set; } = new();
        public List<ProductTurnoverItem> ProductTurnovers { get; set; } = new();

        public string WarehouseNamesJson { get; set; } = "[]";
        public string WarehouseUsedJson { get; set; } = "[]";
        public string WarehouseFreeJson { get; set; } = "[]";
        public string VelocityCountsJson { get; set; } = "[0,0,0]";
    }

    public class WarehouseOccupancyItem
    {
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public int TotalAreas { get; set; }
        public int TotalBins { get; set; }
        public int OccupiedBins { get; set; }
        public int UsedQuantity { get; set; }
        public int MaxCapacity { get; set; }
        public double OccupancyPercent { get; set; }
    }

    public class ProductTurnoverItem
    {
        public int ProductId { get; set; }
        public string? Code { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int ImportedInPeriod { get; set; }
        public int ExportedInPeriod { get; set; }
        public int CurrentStock { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal InventoryValue { get; set; }
        public double AverageStock { get; set; }
        public double TurnoverRate { get; set; }
        public double? DaysSalesOfInventory { get; set; }
        public DateTime? LastExportDate { get; set; }
        public string VelocityCode { get; set; } = string.Empty;
        public string VelocityLabel { get; set; } = string.Empty;
    }
}

