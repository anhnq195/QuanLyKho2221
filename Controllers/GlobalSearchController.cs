using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    /// <summary>
    /// T22 (RQ22 / PB04) - Tìm kiếm nhanh toàn cục toàn bộ sản phẩm, lô hàng, phiếu nhập/xuất/điều chuyển và vị trí ô kệ.
    /// </summary>
    [Authorize]
    public class GlobalSearchController : Controller
    {
        private readonly WarehouseDbContext _context;

        public GlobalSearchController(WarehouseDbContext context)
        {
            _context = context;
        }

        // TRANG KẾT QUẢ TÌM KIẾM ĐẦY ĐỦ
        [HttpGet]
        public async Task<IActionResult> Index(string? q)
        {
            q = q?.Trim();
            ViewBag.Query = q;

            var model = new GlobalSearchResultViewModel { Query = q ?? string.Empty };

            if (string.IsNullOrWhiteSpace(q))
                return View(model);

            bool isAdmin = User.IsInRole("Admin");
            string? currentUser = User.Identity?.Name;

            // 1. Sản phẩm (SKU)
            model.Products = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .Where(p => p.Name.Contains(q) || (p.Code != null && p.Code.Contains(q)))
                .OrderByDescending(p => p.Quantity)
                .Take(20)
                .ToListAsync();

            // 2. Lô hàng
            model.Batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .Where(b => b.BatchCode.Contains(q) ||
                            (b.Product != null && (b.Product.Name.Contains(q) || (b.Product.Code != null && b.Product.Code.Contains(q)))))
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .ToListAsync();

            // 3. Phiếu nhập kho
            var importsQuery = _context.ImportReceipts
                .Include(r => r.Product)
                .Include(r => r.Supplier)
                .AsNoTracking()
                .Where(r => r.ReceiptCode.Contains(q) ||
                            (r.BatchCode != null && r.BatchCode.Contains(q)) ||
                            (r.Product != null && r.Product.Name.Contains(q)) ||
                            (r.Supplier != null && r.Supplier.Name.Contains(q)));

            if (!isAdmin)
                importsQuery = importsQuery.Where(r => r.CreatedBy == currentUser);

            model.Imports = await importsQuery
                .OrderByDescending(r => r.CreatedAt)
                .Take(15)
                .ToListAsync();

            // 4. Phiếu xuất kho
            var exportsQuery = _context.ExportReceipts
                .Include(r => r.Product)
                .Include(r => r.ProductBatch)
                .AsNoTracking()
                .Where(r => r.ReceiptCode.Contains(q) ||
                            (r.Receiver != null && r.Receiver.Contains(q)) ||
                            (r.Product != null && r.Product.Name.Contains(q)) ||
                            (r.ProductBatch != null && r.ProductBatch.BatchCode.Contains(q)));

            if (!isAdmin)
                exportsQuery = exportsQuery.Where(r => r.CreatedBy == currentUser);

            model.Exports = await exportsQuery
                .OrderByDescending(r => r.CreatedAt)
                .Take(15)
                .ToListAsync();

            // 5. Vị trí Ô chứa / Kệ
            model.Bins = await _context.WarehouseBins
                .Include(b => b.ProductBatches)
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .Where(b => b.Code.Contains(q) ||
                            b.Name.Contains(q) ||
                            (b.WarehouseShelf != null && (b.WarehouseShelf.Code.Contains(q) || b.WarehouseShelf.Name.Contains(q))))
                .OrderBy(b => b.Code)
                .Take(15)
                .ToListAsync();

            return View(model);
        }

        // GỢI Ý NHANH TRÊN THANH HEADER - AJAX JSON
        [HttpGet]
        public async Task<IActionResult> Quick(string? q)
        {
            q = q?.Trim();
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new { items = Array.Empty<object>() });

            bool isAdmin = User.IsInRole("Admin");
            string? currentUser = User.Identity?.Name;

            var results = new List<object>();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.Name.Contains(q) || (p.Code != null && p.Code.Contains(q)))
                .Take(4)
                .Select(p => new
                {
                    category = "Sản phẩm",
                    icon = "bi-box-seam text-primary",
                    title = p.Name,
                    sub = $"SKU: {p.Code ?? "N/A"} • Tồn: {p.Quantity}",
                    url = $"/StockCards?productId={p.Id}"
                })
                .ToListAsync();
            results.AddRange(products);

            var batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                .AsNoTracking()
                .Where(b => b.BatchCode.Contains(q))
                .Take(4)
                .Select(b => new
                {
                    category = "Lô hàng",
                    icon = "bi-boxes text-success",
                    title = b.BatchCode,
                    sub = $"{b.Product!.Name} • Tồn: {b.Quantity} • Ô: {b.WarehouseBin!.Code}",
                    url = $"/Scanner?code={Uri.EscapeDataString(b.BatchCode)}"
                })
                .ToListAsync();
            results.AddRange(batches);

            var importsQuery = _context.ImportReceipts
                .Include(r => r.Product)
                .AsNoTracking()
                .Where(r => r.ReceiptCode.Contains(q));
            if (!isAdmin) importsQuery = importsQuery.Where(r => r.CreatedBy == currentUser);

            var imports = await importsQuery
                .Take(3)
                .Select(r => new
                {
                    category = "Phiếu nhập",
                    icon = "bi-box-arrow-in-down text-info",
                    title = r.ReceiptCode,
                    sub = $"{r.Product!.Name} • SL: {r.Quantity} ({r.Status})",
                    url = $"/Imports/Details/{r.Id}"
                })
                .ToListAsync();
            results.AddRange(imports);

            var exportsQuery = _context.ExportReceipts
                .Include(r => r.Product)
                .AsNoTracking()
                .Where(r => r.ReceiptCode.Contains(q));
            if (!isAdmin) exportsQuery = exportsQuery.Where(r => r.CreatedBy == currentUser);

            var exports = await exportsQuery
                .Take(3)
                .Select(r => new
                {
                    category = "Phiếu xuất",
                    icon = "bi-box-arrow-up text-danger",
                    title = r.ReceiptCode,
                    sub = $"{r.Product!.Name} • SL: {r.Quantity} ({r.Status})",
                    url = $"/Exports/Details/{r.Id}"
                })
                .ToListAsync();
            results.AddRange(exports);

            var bins = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                .AsNoTracking()
                .Where(b => b.Code.Contains(q) || b.Name.Contains(q))
                .Take(3)
                .Select(b => new
                {
                    category = "Ô chứa",
                    icon = "bi-grid-3x3-gap text-warning",
                    title = $"Ô {b.Code} - {b.Name}",
                    sub = $"Kệ {b.WarehouseShelf!.Code} • Tầng {b.FloorNumber}",
                    url = $"/WarehouseBins?shelfId={b.WarehouseShelfId}"
                })
                .ToListAsync();
            results.AddRange(bins);

            return Json(new { items = results });
        }
    }

    public class GlobalSearchResultViewModel
    {
        public string Query { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
        public List<ProductBatch> Batches { get; set; } = new();
        public List<ImportReceipt> Imports { get; set; } = new();
        public List<ExportReceipt> Exports { get; set; } = new();
        public List<WarehouseBin> Bins { get; set; } = new();

        public int TotalCount =>
            Products.Count + Batches.Count + Imports.Count + Exports.Count + Bins.Count;
    }
}

