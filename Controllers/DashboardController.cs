
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using System.Text.Json;

namespace QuanLyKho2221.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly WarehouseDbContext _context;

        public DashboardController(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);

            // 1. THỐNG KÊ TỔNG QUAN
            ViewBag.TotalProducts =
                await _context.Products.CountAsync();

            ViewBag.TotalCategories =
                await _context.Categories.CountAsync();

            ViewBag.TotalSuppliers =
                await _context.Suppliers.CountAsync();

            ViewBag.TotalQuantity =
                await _context.Products
                    .SumAsync(p => (int?)p.Quantity) ?? 0;

            // Giá trị tồn kho theo giá sản phẩm hiện có
            ViewBag.TotalInventoryValue =
                await _context.Products
                    .SumAsync(p => (decimal?)(p.Quantity * p.Price)) ?? 0m;

            // 2. SẢN PHẨM SẮP HẾT HÀNG
            // Ngưỡng cảnh báo tạm thời là 5 sản phẩm
            var lowStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Quantity >= 1 && p.Quantity <= 5)
                .OrderBy(p => p.Quantity)
                .ThenBy(p => p.Name)
                .ToListAsync();

            ViewBag.LowStockProducts = lowStockProducts;
            ViewBag.LowStockCount = lowStockProducts.Count;

            // 3. SẢN PHẨM HẾT HÀNG
            var outOfStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Quantity <= 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.OutOfStockProducts = outOfStockProducts;
            ViewBag.OutOfStockCount = outOfStockProducts.Count;

            // 4. TOP 5 SẢN PHẨM TỒN KHO THẤP
            ViewBag.TopLowStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.Quantity <= 5)
                .OrderBy(p => p.Quantity)
                .ThenBy(p => p.Name)
                .Take(5)
                .ToListAsync();

            // 5. PHIẾU NHẬP GẦN ĐÂY
            ViewBag.RecentImports = await _context.ImportReceipts
                .Where(x => x.Status == ReceiptStatus.Approved)
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.Supplier)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 6. PHIẾU XUẤT GẦN ĐÂY
            ViewBag.RecentExports = await _context.ExportReceipts
                .Where(x => x.Status == ReceiptStatus.Approved)
                .AsNoTracking()
                .Include(x => x.Product)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 7. XÁC ĐỊNH 6 THÁNG GẦN NHẤT
            var startDate = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1
            ).AddMonths(-5);

            var endDate = startDate.AddMonths(6);

            // 8. LẤY DỮ LIỆU SỐ LƯỢNG NHẬP
            var imports = await _context.ImportReceipts
                .AsNoTracking()
                .Where(x =>
                    x.Status == ReceiptStatus.Approved &&
                    x.CreatedAt >= startDate &&
                    x.CreatedAt < endDate)
                .Select(x => new
                {
                    x.CreatedAt,
                    x.Quantity
                })
                .ToListAsync();

            // 9. LẤY DỮ LIỆU SỐ LƯỢNG XUẤT
            var exports = await _context.ExportReceipts
                .AsNoTracking()
                .Where(x =>
                    x.Status == ReceiptStatus.Approved &&
                    x.CreatedAt >= startDate &&
                    x.CreatedAt < endDate)
                .Select(x => new
                {
                    x.CreatedAt,
                    x.Quantity
                })
                .ToListAsync();

            // 10. TẠO DANH SÁCH 6 THÁNG
            var months = Enumerable.Range(0, 6)
                .Select(i => startDate.AddMonths(i))
                .ToList();

            var labels = months
                .Select(m => m.ToString("MM/yyyy"))
                .ToList();

            // 11. THỐNG KÊ SỐ LƯỢNG NHẬP
            var importQuantities = months
                .Select(m => imports
                    .Where(x =>
                        x.CreatedAt.Year == m.Year &&
                        x.CreatedAt.Month == m.Month)
                    .Sum(x => x.Quantity))
                .ToList();

            // 12. THỐNG KÊ SỐ LƯỢNG XUẤT
            var exportQuantities = months
                .Select(m => exports
                    .Where(x =>
                        x.CreatedAt.Year == m.Year &&
                        x.CreatedAt.Month == m.Month)
                    .Sum(x => x.Quantity))
                .ToList();

            // 13. TRUYỀN DỮ LIỆU CHO BIỂU ĐỒ
            ViewBag.MonthLabels =
                JsonSerializer.Serialize(labels);

            ViewBag.ImportChartData =
                JsonSerializer.Serialize(importQuantities);

            ViewBag.ExportChartData =
                JsonSerializer.Serialize(exportQuantities);

            return View();
        }
    }
}
