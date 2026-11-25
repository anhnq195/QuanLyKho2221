using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;

namespace QuanLyKho2221.Controllers
{
    /// <summary>
    /// T44 (RQ44 / PB06) - Nhập kho hàng loạt theo lô / container
    /// T56 (RQ56 / PB09) - Nhập danh mục hàng & số dư đầu kỳ từ Excel
    /// T57 (RQ57 / PB09) - Sao lưu dữ liệu tồn kho và chứng từ định kỳ (Backup)
    /// T58 (RQ58 / PB09) - Khôi phục dữ liệu kho khi xảy ra sự cố (Restore)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class DataToolsController : Controller
    {
        private readonly WarehouseDbContext _context;
        private readonly IStockCardService _stockCardService;

        public DataToolsController(
            WarehouseDbContext context,
            IStockCardService stockCardService)
        {
            _context = context;
            _stockCardService = stockCardService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.ProductCount = await _context.Products.CountAsync();
            ViewBag.BatchCount = await _context.ProductBatches.CountAsync();
            ViewBag.ImportCount = await _context.ImportReceipts.CountAsync();
            ViewBag.ExportCount = await _context.ExportReceipts.CountAsync();
            ViewBag.StockCardCount = await _context.StockCards.CountAsync();
            return View();
        }

        // TẢI FILE EXCEL MẪU ĐỂ NHẬP HÀNG LOẠT (T56, T44)
        [HttpGet]
        public async Task<IActionResult> DownloadTemplate()
        {
            var bins = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .ToListAsync();

            string sampleBinCode = bins.FirstOrDefault()?.Code ?? "O01";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("NhapHangLoat");

            string[] headers =
            {
                "Mã SKU (Tùy chọn)",
                "Tên sản phẩm (*)",
                "Danh mục",
                "Đơn giá (VNĐ)",
                "Mã ô chứa (*)",
                "Mã lô hàng (*)",
                "Số lượng (*)",
                "Ngày SX (yyyy-MM-dd)",
                "Hạn SD (yyyy-MM-dd)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = XLColor.White;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
            }

            // 2 Dòng dữ liệu mẫu
            ws.Cell(2, 1).Value = "SP-DEMO01";
            ws.Cell(2, 2).Value = "Sữa tươi tiệt trùng TH True Milk 1L";
            ws.Cell(2, 3).Value = "Thực phẩm & Đồ uống";
            ws.Cell(2, 4).Value = 36000;
            ws.Cell(2, 5).Value = sampleBinCode;
            ws.Cell(2, 6).Value = $"LOT-{DateTime.Now:yyyyMMdd}-A01";
            ws.Cell(2, 7).Value = 50;
            ws.Cell(2, 8).Value = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
            ws.Cell(2, 9).Value = DateTime.Today.AddMonths(5).ToString("yyyy-MM-dd");

            ws.Cell(3, 1).Value = "SP-DEMO02";
            ws.Cell(3, 2).Value = "Bàn phím cơ không dây Keychron K8";
            ws.Cell(3, 3).Value = "Thiết bị điện tử";
            ws.Cell(3, 4).Value = 1850000;
            ws.Cell(3, 5).Value = sampleBinCode;
            ws.Cell(3, 6).Value = $"LOT-{DateTime.Now:yyyyMMdd}-B02";
            ws.Cell(3, 7).Value = 25;
            ws.Cell(3, 8).Value = DateTime.Today.AddMonths(-2).ToString("yyyy-MM-dd");
            ws.Cell(3, 9).Value = "";

            ws.Columns().AdjustToContents();

            // Sheet 2: Danh mục mã ô chứa hiện có
            var wsBins = wb.Worksheets.Add("DanhSachMaOChua");
            wsBins.Cell(1, 1).Value = "Mã ô chứa";
            wsBins.Cell(1, 2).Value = "Tên ô";
            wsBins.Cell(1, 3).Value = "Vị trí Kho / Khu / Kệ";
            wsBins.Cell(1, 4).Value = "Sức chứa tối đa";
            wsBins.Range(1, 1, 1, 4).Style.Font.Bold = true;

            int row = 2;
            foreach (var b in bins)
            {
                wsBins.Cell(row, 1).Value = b.Code;
                wsBins.Cell(row, 2).Value = b.Name;
                wsBins.Cell(row, 3).Value =
                    $"{b.WarehouseShelf?.WarehouseArea?.Warehouse?.Name} > {b.WarehouseShelf?.WarehouseArea?.Name} > {b.WarehouseShelf?.Name}";
                wsBins.Cell(row, 4).Value = b.MaxCapacity;
                row++;
            }
            wsBins.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Mau_NhapHangLoat_WMS.xlsx");
        }

        // XỬ LÝ NHẬP DANH MỤC & SỐ DƯ ĐẦU KỲ TỪ EXCEL (T56, T44)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile? excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel (.xlsx) hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (!Path.GetExtension(excelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Hệ thống chỉ hỗ trợ định dạng file .xlsx.";
                return RedirectToAction(nameof(Index));
            }

            int importedRows = 0;
            var errors = new List<string>();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                var defaultBin = await _context.WarehouseBins.OrderBy(b => b.Id).FirstOrDefaultAsync();
                if (defaultBin == null)
                {
                    TempData["Error"] = "Hệ thống chưa có ô chứa nào. Vui lòng tạo Kho/Kệ/Ô chứa trước khi import.";
                    return RedirectToAction(nameof(Index));
                }

                for (int r = 2; r <= lastRow; r++)
                {
                    string sku = ws.Cell(r, 1).GetString().Trim();
                    string productName = ws.Cell(r, 2).GetString().Trim();
                    string categoryName = ws.Cell(r, 3).GetString().Trim();
                    decimal.TryParse(ws.Cell(r, 4).GetString().Trim(), out decimal price);
                    string binCode = ws.Cell(r, 5).GetString().Trim();
                    string batchCode = ws.Cell(r, 6).GetString().Trim();
                    int.TryParse(ws.Cell(r, 7).GetString().Trim(), out int qty);
                    string mfgStr = ws.Cell(r, 8).GetString().Trim();
                    string expStr = ws.Cell(r, 9).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(productName) && string.IsNullOrWhiteSpace(batchCode))
                        continue;

                    if (string.IsNullOrWhiteSpace(productName) || qty <= 0)
                    {
                        errors.Add($"Dòng {r}: Thiếu tên sản phẩm hoặc số lượng <= 0.");
                        continue;
                    }

                    // 1. Danh mục
                    Category? category = null;
                    if (!string.IsNullOrWhiteSpace(categoryName))
                    {
                        category = await _context.Categories
                            .FirstOrDefaultAsync(c => c.Name == categoryName);
                        if (category == null)
                        {
                            category = new Category { Name = categoryName, Description = "Tạo tự động từ Import Excel" };
                            _context.Categories.Add(category);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // 2. Sản phẩm
                    Product? product = null;
                    if (!string.IsNullOrWhiteSpace(sku))
                    {
                        product = await _context.Products.FirstOrDefaultAsync(p => p.Code == sku);
                    }
                    if (product == null)
                    {
                        product = await _context.Products.FirstOrDefaultAsync(p => p.Name == productName);
                    }

                    DateTime? mfgDate = DateTime.TryParse(mfgStr, out var md) ? md : null;
                    DateTime? expDate = DateTime.TryParse(expStr, out var ed) ? ed : null;

                    if (product == null)
                    {
                        product = new Product
                        {
                            Code = string.IsNullOrWhiteSpace(sku)
                                ? "SP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()
                                : sku,
                            Name = productName,
                            CategoryId = category?.Id,
                            Price = price > 0 ? price : 0,
                            HasExpiryDate = expDate.HasValue,
                            Quantity = 0,
                            CreatedAt = DateTime.Now
                        };
                        _context.Products.Add(product);
                        await _context.SaveChangesAsync();
                    }

                    // 3. Ô chứa
                    var bin = !string.IsNullOrWhiteSpace(binCode)
                        ? await _context.WarehouseBins.FirstOrDefaultAsync(b => b.Code == binCode) ?? defaultBin
                        : defaultBin;

                    if (string.IsNullOrWhiteSpace(batchCode))
                    {
                        batchCode = $"LOT-{DateTime.Now:yyyyMMdd}-" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
                    }

                    // 4. Lô hàng
                    var existingBatch = await _context.ProductBatches
                        .FirstOrDefaultAsync(b =>
                            b.ProductId == product.Id &&
                            b.WarehouseBinId == bin.Id &&
                            b.BatchCode == batchCode);

                    if (existingBatch != null)
                    {
                        existingBatch.Quantity += qty;
                    }
                    else
                    {
                        existingBatch = new ProductBatch
                        {
                            ProductId = product.Id,
                            WarehouseBinId = bin.Id,
                            BatchCode = batchCode,
                            ManufactureDate = mfgDate,
                            ExpiryDate = expDate,
                            Quantity = qty,
                            CreatedAt = DateTime.Now
                        };
                        _context.ProductBatches.Add(existingBatch);
                    }
                    await _context.SaveChangesAsync();

                    product.Quantity = await _context.ProductBatches
                        .Where(b => b.ProductId == product.Id)
                        .SumAsync(b => b.Quantity);

                    await _stockCardService.RecordEntryAsync(
                        receiptCode: "EXL-" + DateTime.Now.ToString("MMdd"),
                        transactionType: "Nhập Excel đầu kỳ",
                        productId: product.Id,
                        productBatchId: existingBatch.Id,
                        warehouseBinId: bin.Id,
                        quantityChange: qty,
                        unitPrice: product.Price,
                        note: $"Import hàng loạt từ file {excelFile.FileName} (Lô {batchCode})",
                        createdBy: User.Identity?.Name ?? "Admin");

                    importedRows++;
                }

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Admin",
                    Action = "Import Excel tồn kho",
                    Description = $"Đã nhập thành công {importedRows} dòng lô hàng từ file {excelFile.FileName}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Đã nhập kho thành công {importedRows} dòng lô hàng từ file Excel! Tồn kho và Thẻ kho đã được cập nhật.";
                if (errors.Any())
                {
                    TempData["Warning"] = string.Join(" | ", errors.Take(5));
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi khi đọc file Excel: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // SAO LƯU DỮ LIỆU KHO RA FILE JSON CHUẨN (T57 - BACKUP WAREHOUSE DATA)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportBackup()
        {
            var payload = new WarehouseBackupPackage
            {
                BackupVersion = "1.0",
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity?.Name ?? "Admin",
                Categories = await _context.Categories.AsNoTracking().ToListAsync(),
                Suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(),
                Products = await _context.Products.AsNoTracking().ToListAsync(),
                Warehouses = await _context.Warehouses.AsNoTracking().ToListAsync(),
                Batches = await _context.ProductBatches.AsNoTracking().ToListAsync()
            };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string rawJson = JsonSerializer.Serialize(payload, jsonOptions);
            byte[] bytes = Encoding.UTF8.GetBytes(rawJson);

            string checksum = Convert.ToHexString(SHA256.HashData(bytes));
            payload.Sha256Checksum = checksum;

            bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, jsonOptions));

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Admin",
                Action = "Sao lưu dữ liệu kho",
                Description = $"Xuất gói sao lưu dữ liệu kho (SHA256: {checksum[..12]}...)",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            string fileName = $"WMS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            return File(bytes, "application/json", fileName);
        }

        // KHÔI PHỤC DỮ LIỆU KHO TỪ GÓI SAO LƯU (T58 - RESTORE WAREHOUSE DATA)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBackup(IFormFile? backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file sao lưu (.json) hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var reader = new StreamReader(backupFile.OpenReadStream(), Encoding.UTF8);
                string content = await reader.ReadToEndAsync();

                var pkg = JsonSerializer.Deserialize<WarehouseBackupPackage>(content);
                if (pkg == null)
                {
                    TempData["Error"] = "Cấu trúc gói sao lưu không hợp lệ.";
                    return RedirectToAction(nameof(Index));
                }

                int restoredCategories = 0;
                int restoredSuppliers = 0;
                int restoredProducts = 0;

                foreach (var cat in pkg.Categories)
                {
                    if (!await _context.Categories.AnyAsync(c => c.Name == cat.Name))
                    {
                        _context.Categories.Add(new Category
                        {
                            Name = cat.Name,
                            Description = cat.Description
                        });
                        restoredCategories++;
                    }
                }
                await _context.SaveChangesAsync();

                foreach (var sup in pkg.Suppliers)
                {
                    if (!await _context.Suppliers.AnyAsync(s => s.Name == sup.Name))
                    {
                        _context.Suppliers.Add(new Supplier
                        {
                            Name = sup.Name,
                            Phone = sup.Phone,
                            Email = sup.Email,
                            Address = sup.Address,
                            CreatedAt = DateTime.Now
                        });
                        restoredSuppliers++;
                    }
                }
                await _context.SaveChangesAsync();

                foreach (var prod in pkg.Products)
                {
                    bool exists = (!string.IsNullOrWhiteSpace(prod.Code) && await _context.Products.AnyAsync(p => p.Code == prod.Code))
                                  || await _context.Products.AnyAsync(p => p.Name == prod.Name);

                    if (!exists)
                    {
                        _context.Products.Add(new Product
                        {
                            Code = prod.Code,
                            Name = prod.Name,
                            Description = prod.Description,
                            Price = prod.Price,
                            HasExpiryDate = prod.HasExpiryDate,
                            Quantity = 0,
                            CreatedAt = DateTime.Now
                        });
                        restoredProducts++;
                    }
                }

                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = User.Identity?.Name ?? "Admin",
                    Action = "Khôi phục dữ liệu kho",
                    Description = $"Khôi phục từ file {backupFile.FileName}: +{restoredCategories} danh mục, +{restoredSuppliers} NCC, +{restoredProducts} sản phẩm.",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"Khôi phục dữ liệu thành công từ bản sao lưu ({pkg.CreatedAt:dd/MM/yyyy HH:mm})! " +
                    $"Đã phục hồi bổ sung: {restoredCategories} danh mục, {restoredSuppliers} nhà cung cấp, {restoredProducts} sản phẩm.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể khôi phục từ file sao lưu: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class WarehouseBackupPackage
    {
        public string BackupVersion { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Sha256Checksum { get; set; } = string.Empty;
        public List<Category> Categories { get; set; } = new();
        public List<Supplier> Suppliers { get; set; } = new();
        public List<Product> Products { get; set; } = new();
        public List<Warehouse> Warehouses { get; set; } = new();
        public List<ProductBatch> Batches { get; set; } = new();
    }
}
