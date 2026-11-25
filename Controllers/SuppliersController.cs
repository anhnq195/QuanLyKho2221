
using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SuppliersController : Controller
    {
        private readonly WarehouseDbContext _context;

        public SuppliersController(WarehouseDbContext context)
        {
            _context = context;
        }

        // GHI NHẬT KÝ HOẠT ĐỘNG
        private void AddActivityLog(string action, string description)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = action,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        // DANH SÁCH
        public async Task<IActionResult> Index(string? searchString)
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            var suppliers = _context.Suppliers
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Product)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string keyword = searchString.Trim();

                suppliers = suppliers.Where(s =>
                    s.Name.Contains(keyword) ||
                    (s.Phone != null && s.Phone.Contains(keyword)));
            }

            ViewBag.SearchString = searchString;

            return View(await suppliers
                .OrderByDescending(s => s.Id)
                .ToListAsync());
        }

        private async Task LoadProductsForSupplierAsync(IEnumerable<int>? selectedIds = null)
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);
            ViewBag.AllProducts = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();
            ViewBag.SelectedProductIds = new HashSet<int>(selectedIds ?? Enumerable.Empty<int>());
        }

        // FORM THÊM
        public async Task<IActionResult> Create()
        {
            await LoadProductsForSupplierAsync();
            return View();
        }

        // XỬ LÝ THÊM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier, int[]? productIds)
        {
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            if (ModelState.IsValid)
            {
                supplier.CreatedAt = DateTime.Now;

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                if (productIds != null && productIds.Length > 0)
                {
                    foreach (var pid in productIds.Distinct())
                    {
                        _context.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = supplier.Id,
                            ProductId = pid,
                            CreatedAt = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                AddActivityLog(
                    "Thêm nhà cung cấp",
                    $"Đã thêm nhà cung cấp: {supplier.Name}, số điện thoại: {supplier.Phone}, số SP cung ứng: {productIds?.Length ?? 0}."
                );

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            await LoadProductsForSupplierAsync(productIds);
            return View(supplier);
        }

        // FORM SỬA
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            var supplier = await _context.Suppliers
                .Include(s => s.SupplierProducts)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return NotFound();

            var selectedIds = supplier.SupplierProducts.Select(sp => sp.ProductId).ToList();
            await LoadProductsForSupplierAsync(selectedIds);

            return View(supplier);
        }

        // XỬ LÝ SỬA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier input, int[]? productIds)
        {
            if (id != input.Id)
                return NotFound();

            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            if (ModelState.IsValid)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.SupplierProducts)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                    return NotFound();

                string oldName = supplier.Name;

                supplier.Name = input.Name;
                supplier.Phone = input.Phone;
                supplier.Email = input.Email;
                supplier.Address = input.Address;
                supplier.Description = input.Description;

                // Cập nhật danh sách sản phẩm mà nhà cung cấp này được phép cung cấp
                _context.SupplierProducts.RemoveRange(supplier.SupplierProducts);
                if (productIds != null && productIds.Length > 0)
                {
                    foreach (var pid in productIds.Distinct())
                    {
                        _context.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = supplier.Id,
                            ProductId = pid,
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                AddActivityLog(
                    "Sửa nhà cung cấp",
                    $"Đã cập nhật nhà cung cấp ID {id}: {oldName} → {supplier.Name}. Số SP cung ứng: {productIds?.Length ?? 0}."
                );

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            await LoadProductsForSupplierAsync(productIds);
            return View(input);
        }

        // FORM XÓA
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return NotFound();

            return View(supplier);
        }

        // XỬ LÝ XÓA
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);

            if (supplier != null)
            {
                string supplierName = supplier.Name;

                _context.Suppliers.Remove(supplier);

                AddActivityLog(
                    "Xóa nhà cung cấp",
                    $"Đã xóa nhà cung cấp: {supplierName} (ID: {id})."
                );

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // XUẤT DANH SÁCH NHÀ CUNG CẤP & LỊCH SỬ GIAO HÀNG RA EXCEL (T43)
        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            var stats = await _context.ImportReceipts
                .AsNoTracking()
                .GroupBy(r => r.SupplierId)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    TotalReceipts = g.Count(),
                    ApprovedQuantity = g.Where(x => x.Status == ReceiptStatus.Approved).Sum(x => x.Quantity),
                    LastDelivery = g.Max(x => (DateTime?)x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.SupplierId);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("NhaCungCap");

            ws.Cell(1, 1).Value = "DANH SÁCH NHÀ CUNG CẤP & LỊCH SỬ GIAO HÀNG";
            ws.Range(1, 1, 1, 8).Merge();
            var title = ws.Range(1, 1, 1, 8);
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 30;

            string[] headers = { "ID", "Tên nhà cung cấp", "Số điện thoại", "Email", "Địa chỉ", "Tổng số phiếu nhập", "Tổng SL đã giao", "Lần giao gần nhất" };
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(3, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int r = 4;
            foreach (var s in suppliers)
            {
                stats.TryGetValue(s.Id, out var st);
                ws.Cell(r, 1).Value = s.Id;
                ws.Cell(r, 2).Value = s.Name;
                ws.Cell(r, 3).Value = s.Phone ?? "";
                ws.Cell(r, 4).Value = s.Email ?? "";
                ws.Cell(r, 5).Value = s.Address ?? "";
                ws.Cell(r, 6).Value = st?.TotalReceipts ?? 0;
                ws.Cell(r, 7).Value = st?.ApprovedQuantity ?? 0;
                ws.Cell(r, 8).Value = st?.LastDelivery?.ToString("dd/MM/yyyy") ?? "Chưa có";
                ws.Range(r, 1, r, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"NhaCungCap_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }
    }
}
