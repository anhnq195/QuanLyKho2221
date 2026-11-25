using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    /// <summary>
    /// T23  - Mobile Scanner UI (quét Barcode/QR bằng camera điện thoại hoặc súng quét USB)
    /// T111 - Tự động sinh tem Barcode/QR cho lô hàng và ô chứa
    /// </summary>
    [Authorize(Roles = "Admin,Employee")]
    public class ScannerController : Controller
    {
        public const string BatchPrefix = "WMS-B";
        public const string BinPrefix = "WMS-L";

        private static readonly Regex BatchPattern = new(@"^WMS-B(\d+)$", RegexOptions.IgnoreCase);
        private static readonly Regex BinPattern = new(@"^WMS-L(\d+)$", RegexOptions.IgnoreCase);

        private readonly WarehouseDbContext _context;

        public ScannerController(WarehouseDbContext context)
        {
            _context = context;
        }

        // GIAO DIỆN QUÉT MÃ (MOBILE)
        public IActionResult Index()
        {
            return View();
        }

        // TRA CỨU MÃ VỪA QUÉT - AJAX
        [HttpGet]
        public async Task<IActionResult> Lookup(string? code)
        {
            code = code?.Trim();

            if (string.IsNullOrWhiteSpace(code))
                return Json(new { found = false, message = "Mã quét trống." });

            if (code.Length > 100)
                return Json(new { found = false, message = "Mã quét không hợp lệ." });

            // 1. Tem lô hàng WMS-B{id}
            var batchMatch = BatchPattern.Match(code);
            if (batchMatch.Success && int.TryParse(batchMatch.Groups[1].Value, out int batchId))
            {
                var batch = await BatchQuery().FirstOrDefaultAsync(b => b.Id == batchId);
                if (batch == null)
                    return Json(new { found = false, message = $"Không tìm thấy lô hàng có mã tem {code}." });

                // Cùng mã lô có thể nằm ở nhiều ô sau khi điều chuyển
                var sameCode = await BatchQuery()
                    .Where(b => b.BatchCode == batch.BatchCode && b.Id != batch.Id)
                    .ToListAsync();

                var all = new List<ProductBatch> { batch };
                all.AddRange(sameCode);

                return Json(new
                {
                    found = true,
                    type = "batch",
                    title = $"Lô {batch.BatchCode}",
                    product = MapProduct(batch.Product),
                    batches = await MapBatchesAsync(all)
                });
            }

            // 2. Tem ô chứa WMS-L{id}
            var binMatch = BinPattern.Match(code);
            if (binMatch.Success && int.TryParse(binMatch.Groups[1].Value, out int binId))
            {
                return await BinResultAsync(binId, code);
            }

            // 3. Mã lô gõ tay / in trên bao bì nhà cung cấp
            var byBatchCode = await BatchQuery()
                .Where(b => b.BatchCode == code)
                .ToListAsync();

            if (byBatchCode.Count > 0)
            {
                return Json(new
                {
                    found = true,
                    type = "batch",
                    title = $"Lô {code}",
                    product = MapProduct(byBatchCode[0].Product),
                    batches = await MapBatchesAsync(byBatchCode)
                });
            }

            // 4. Mã SKU sản phẩm
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == code);

            if (product != null)
            {
                var productBatches = await BatchQuery()
                    .Where(b => b.ProductId == product.Id && b.Quantity > 0)
                    .ToListAsync();

                return Json(new
                {
                    found = true,
                    type = "product",
                    title = product.Name,
                    product = MapProduct(product),
                    batches = await MapBatchesAsync(productBatches)
                });
            }

            // 5. Mã ô chứa gõ tay (chỉ nhận khi duy nhất)
            var bins = await _context.WarehouseBins
                .AsNoTracking()
                .Where(b => b.Code == code)
                .Select(b => b.Id)
                .ToListAsync();

            if (bins.Count == 1)
                return await BinResultAsync(bins[0], code);

            if (bins.Count > 1)
                return Json(new
                {
                    found = false,
                    message = $"Có {bins.Count} ô chứa cùng mã {code} ở các kệ khác nhau. Vui lòng quét tem ô chứa (WMS-L...) để xác định chính xác."
                });

            return Json(new { found = false, message = $"Không tìm thấy lô hàng, sản phẩm hoặc ô chứa nào khớp với mã \"{code}\"." });
        }

        // IN TEM LÔ HÀNG / Ô CHỨA
        // /Scanner/Labels?batchIds=1,2,3   hoặc   /Scanner/Labels?shelfId=5
        [HttpGet]
        public async Task<IActionResult> Labels(string? batchIds, int? shelfId, int copies = 1)
        {
            copies = Math.Clamp(copies, 1, 50);
            var labels = new List<LabelViewModel>();

            if (!string.IsNullOrWhiteSpace(batchIds))
            {
                var ids = batchIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x, out var v) ? v : 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .Take(500)
                    .ToList();

                var batches = await BatchQuery()
                    .Where(b => ids.Contains(b.Id))
                    .OrderBy(b => b.Product!.Name)
                    .ToListAsync();

                foreach (var b in batches)
                {
                    labels.Add(new LabelViewModel
                    {
                        Payload = BatchPrefix + b.Id,
                        Title = b.Product?.Name ?? "Sản phẩm",
                        SubTitle = $"Lô: {b.BatchCode}",
                        Line1 = $"SKU: {b.Product?.Code ?? "N/A"}",
                        Line2 = b.ExpiryDate.HasValue
                            ? $"HSD: {b.ExpiryDate:dd/MM/yyyy}"
                            : (b.ManufactureDate.HasValue ? $"NSX: {b.ManufactureDate:dd/MM/yyyy}" : "Không có HSD"),
                        Line3 = BuildLocation(b.WarehouseBin)
                    });
                }
            }
            else if (shelfId.HasValue)
            {
                var bins = await _context.WarehouseBins
                    .Include(b => b.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                    .AsNoTracking()
                    .Where(b => b.WarehouseShelfId == shelfId.Value)
                    .OrderBy(b => b.FloorNumber)
                    .ThenBy(b => b.Code)
                    .ToListAsync();

                foreach (var bin in bins)
                {
                    labels.Add(new LabelViewModel
                    {
                        Payload = BinPrefix + bin.Id,
                        Title = $"Ô {bin.Code}",
                        SubTitle = bin.Name,
                        Line1 = $"Kệ: {bin.WarehouseShelf?.Code} - Tầng {bin.FloorNumber}",
                        Line2 = $"Khu: {bin.WarehouseShelf?.WarehouseArea?.Name}",
                        Line3 = bin.WarehouseShelf?.WarehouseArea?.Warehouse?.Name ?? ""
                    });
                }
            }

            if (copies > 1)
            {
                labels = labels.SelectMany(l => Enumerable.Repeat(l, copies)).ToList();
            }

            return View(labels);
        }

        // ===================== HELPERS =====================

        private IQueryable<ProductBatch> BatchQuery()
        {
            return _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking();
        }

        private async Task<IActionResult> BinResultAsync(int binId, string code)
        {
            var bin = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == binId);

            if (bin == null)
                return Json(new { found = false, message = $"Không tìm thấy ô chứa có mã tem {code}." });

            var binBatches = await BatchQuery()
                .Where(b => b.WarehouseBinId == bin.Id && b.Quantity > 0)
                .ToListAsync();

            int used = binBatches.Sum(b => b.Quantity);
            int capacity = bin.MaxCapacity > 0 ? bin.MaxCapacity : 1000;

            return Json(new
            {
                found = true,
                type = "bin",
                title = $"Ô chứa {bin.Code}",
                bin = new
                {
                    bin.Id,
                    bin.Code,
                    bin.Name,
                    bin.FloorNumber,
                    location = BuildLocation(bin),
                    used,
                    capacity,
                    fillPercent = (int)Math.Min(100, Math.Round(used * 100.0 / capacity))
                },
                batches = await MapBatchesAsync(binBatches)
            });
        }

        private static object? MapProduct(Product? p)
        {
            if (p == null) return null;
            return new
            {
                p.Id,
                p.Name,
                p.Code,
                p.Quantity,
                p.HasExpiryDate
            };
        }

        private async Task<List<object>> MapBatchesAsync(List<ProductBatch> batches)
        {
            var ids = batches.Select(b => b.Id).ToList();

            var pending = await _context.ExportReceipts
                .AsNoTracking()
                .Where(x => x.Status == ReceiptStatus.Pending &&
                            x.ProductBatchId.HasValue &&
                            ids.Contains(x.ProductBatchId.Value))
                .GroupBy(x => x.ProductBatchId!.Value)
                .Select(g => new { BatchId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.BatchId, x => x.Qty);

            var today = DateTime.Today;

            return batches.Select(b =>
            {
                pending.TryGetValue(b.Id, out int pendingQty);

                string expiryStatus = "none";
                int? daysLeft = null;
                if (b.ExpiryDate.HasValue)
                {
                    daysLeft = (b.ExpiryDate.Value.Date - today).Days;
                    expiryStatus = daysLeft < 0 ? "expired" : (daysLeft <= 30 ? "near" : "ok");
                }

                return (object)new
                {
                    b.Id,
                    labelCode = BatchPrefix + b.Id,
                    b.BatchCode,
                    productId = b.ProductId,
                    productName = b.Product?.Name,
                    productCode = b.Product?.Code,
                    b.Quantity,
                    pending = pendingQty,
                    available = Math.Max(0, b.Quantity - pendingQty),
                    manufactureDate = b.ManufactureDate?.ToString("dd/MM/yyyy"),
                    expiryDate = b.ExpiryDate?.ToString("dd/MM/yyyy"),
                    expiryStatus,
                    daysLeft,
                    binId = b.WarehouseBinId,
                    binCode = b.WarehouseBin?.Code,
                    location = BuildLocation(b.WarehouseBin)
                };
            }).ToList();
        }

        private static string BuildLocation(WarehouseBin? bin)
        {
            if (bin == null) return "Chưa xác định vị trí";

            var shelf = bin.WarehouseShelf;
            var area = shelf?.WarehouseArea;
            var wh = area?.Warehouse;

            return string.Join(" › ", new[]
            {
                wh?.Name,
                area?.Name,
                shelf != null ? $"Kệ {shelf.Code}" : null,
                $"Tầng {bin.FloorNumber}",
                $"Ô {bin.Code}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    public class LabelViewModel
    {
        public string Payload { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string Line3 { get; set; } = string.Empty;
    }
}

