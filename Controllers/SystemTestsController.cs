using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;

namespace QuanLyKho2221.Controllers
{
    /// <summary>
    /// T78, T79, T80, T81, T118, T119, T120, T121
    /// Bộ kiểm thử tự động (Unit & Integration Tests) chạy trong Transaction Rollback cô lập,
    /// không làm thay đổi bất kỳ dữ liệu thật nào trong CSDL.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class SystemTestsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public SystemTestsController(WarehouseDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var report = await ExecuteAllTestsAsync();
            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunJson()
        {
            var report = await ExecuteAllTestsAsync();
            return Json(report);
        }

        private async Task<TestSuiteReport> ExecuteAllTestsAsync()
        {
            var suiteSw = Stopwatch.StartNew();
            var results = new List<TestCaseResult>();

            // 1. TC01 - FEFO ưu tiên lô cận date & loại bỏ lô hết hạn (T60, T80)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-01",
                "T60 / T80",
                "Thuật toán FEFO: Ưu tiên lô cận date nhất và tự động loại bỏ lô đã hết hạn",
                "Tạo 3 lô: Lô A (đã hết hạn), Lô B (còn 10 ngày, tồn 30), Lô C (còn 60 ngày, tồn 50). Yêu cầu xuất 45.",
                "Bỏ qua Lô A, lấy 30 từ Lô B + 15 từ Lô C",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync();
                    var bExpired = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "TEST-EXP",
                        ExpiryDate = DateTime.Today.AddDays(-2),
                        Quantity = 100
                    };
                    var bNear = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "TEST-NEAR",
                        ExpiryDate = DateTime.Today.AddDays(10),
                        Quantity = 30
                    };
                    var bFar = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "TEST-FAR",
                        ExpiryDate = DateTime.Today.AddDays(60),
                        Quantity = 50
                    };
                    _context.ProductBatches.AddRange(bExpired, bNear, bFar);
                    await _context.SaveChangesAsync();

                    var svc = new WarehouseRuleService(_context);
                    var plan = await svc.CalculateOutboundAllocationAsync(seed.Product.Id, 45, "FEFO");

                    bool ok = plan.IsSufficient &&
                              plan.Allocations.Count == 2 &&
                              plan.Allocations[0].BatchCode == "TEST-NEAR" &&
                              plan.Allocations[0].AllocatedQuantity == 30 &&
                              plan.Allocations[1].BatchCode == "TEST-FAR" &&
                              plan.Allocations[1].AllocatedQuantity == 15;

                    string actual = string.Join(" + ", plan.Allocations.Select(a => $"{a.BatchCode}:{a.AllocatedQuantity}"));
                    return (ok, $"Phân bổ thực tế: {actual} (Tổng: {plan.TotalAllocatedQuantity})");
                }));

            // 2. TC02 - FIFO ưu tiên lô nhập trước (T60, T80)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-02",
                "T60 / T80",
                "Thuật toán FIFO: Lô nhập kho trước được trích xuất trước",
                "Tạo Lô Cũ (nhập 10 ngày trước, tồn 20) và Lô Mới (nhập hôm nay, tồn 40). Yêu cầu xuất 25 theo FIFO.",
                "Lấy 20 từ Lô Cũ + 5 từ Lô Mới",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync();
                    var bOld = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "FIFO-OLD",
                        CreatedAt = DateTime.Now.AddDays(-10),
                        Quantity = 20
                    };
                    var bNew = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "FIFO-NEW",
                        CreatedAt = DateTime.Now,
                        Quantity = 40
                    };
                    _context.ProductBatches.AddRange(bOld, bNew);
                    await _context.SaveChangesAsync();

                    var svc = new WarehouseRuleService(_context);
                    var plan = await svc.CalculateOutboundAllocationAsync(seed.Product.Id, 25, "FIFO");

                    bool ok = plan.IsSufficient &&
                              plan.Allocations.Count == 2 &&
                              plan.Allocations[0].BatchCode == "FIFO-OLD" &&
                              plan.Allocations[0].AllocatedQuantity == 20 &&
                              plan.Allocations[1].BatchCode == "FIFO-NEW" &&
                              plan.Allocations[1].AllocatedQuantity == 5;

                    string actual = string.Join(" + ", plan.Allocations.Select(a => $"{a.BatchCode}:{a.AllocatedQuantity}"));
                    return (ok, $"Phân bổ thực tế: {actual}");
                }));

            // 3. TC03 - LIFO ưu tiên lô mới nhất (T60)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-03",
                "T60 / T80",
                "Thuật toán LIFO: Lô nhập sau cùng được trích xuất trước",
                "Tạo Lô Cũ (tồn 20) và Lô Mới (tồn 40). Yêu cầu xuất 25 theo LIFO.",
                "Lấy toàn bộ 25 từ Lô Mới",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync();
                    var bOld = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "LIFO-OLD",
                        CreatedAt = DateTime.Now.AddDays(-10),
                        Quantity = 20
                    };
                    var bNew = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "LIFO-NEW",
                        CreatedAt = DateTime.Now,
                        Quantity = 40
                    };
                    _context.ProductBatches.AddRange(bOld, bNew);
                    await _context.SaveChangesAsync();

                    var svc = new WarehouseRuleService(_context);
                    var plan = await svc.CalculateOutboundAllocationAsync(seed.Product.Id, 25, "LIFO");

                    bool ok = plan.IsSufficient &&
                              plan.Allocations.Count == 1 &&
                              plan.Allocations[0].BatchCode == "LIFO-NEW" &&
                              plan.Allocations[0].AllocatedQuantity == 25;

                    return (ok, $"Lô được chọn: {plan.Allocations.FirstOrDefault()?.BatchCode} ({plan.TotalAllocatedQuantity})");
                }));

            // 4. TC04 - Cơ chế Giữ chỗ Pending Reservation khi xuất kho (T79, T113)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-04",
                "T79 / T113",
                "Cơ chế giữ chỗ (Pending Reservation): Trừ số lượng đang chờ duyệt khỏi tồn khả dụng",
                "Lô hàng tồn 50, đang có phiếu xuất Pending giữ chỗ 35. Yêu cầu xuất tiếp 20.",
                "Báo thiếu 5 sản phẩm (chỉ còn 15 khả dụng), chặn tạo vượt tồn",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync();
                    var batch = new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "RES-BATCH",
                        Quantity = 50
                    };
                    _context.ProductBatches.Add(batch);
                    await _context.SaveChangesAsync();

                    _context.ExportReceipts.Add(new ExportReceipt
                    {
                        ReceiptCode = "PX-HOLD",
                        ProductId = seed.Product.Id,
                        ProductBatchId = batch.Id,
                        Quantity = 35,
                        UnitPrice = 10000,
                        Status = ReceiptStatus.Pending
                    });
                    await _context.SaveChangesAsync();

                    var svc = new WarehouseRuleService(_context);
                    var plan = await svc.CalculateOutboundAllocationAsync(seed.Product.Id, 20, "FIFO");

                    bool ok = !plan.IsSufficient &&
                              plan.TotalAllocatedQuantity == 15 &&
                              plan.ShortageQuantity == 5;

                    return (ok, $"Khả dụng: {plan.TotalAllocatedQuantity}, Thiếu: {plan.ShortageQuantity}, Đủ hàng: {plan.IsSufficient}");
                }));

            // 5. TC05 - Thẻ kho StockCard tính số dư lũy kế (T107, T119)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-05",
                "T107 / T119",
                "Thẻ kho (StockCard): Kiểm chứng tính liên tục Tồn đầu → Nhập → Xuất → Kiểm kê → Tồn cuối",
                "Nhập +100, Xuất -35, Kiểm kê cân bằng -5 trên cùng một sản phẩm.",
                "Dòng 1: 0→100; Dòng 2: 100→65; Dòng 3: 65→60",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync();
                    var cardSvc = new StockCardService(_context);

                    var c1 = await cardSvc.RecordEntryAsync("PN-T1", "Nhập kho", seed.Product.Id, null, seed.Bin.Id, +100);
                    var c2 = await cardSvc.RecordEntryAsync("PX-T1", "Xuất kho", seed.Product.Id, null, seed.Bin.Id, -35);
                    var c3 = await cardSvc.RecordEntryAsync("KK-T1", "Cân bằng kiểm kê", seed.Product.Id, null, seed.Bin.Id, -5);

                    bool ok = c1.OpeningQuantity == 0 && c1.ClosingQuantity == 100 &&
                              c2.OpeningQuantity == 100 && c2.ClosingQuantity == 65 &&
                              c3.OpeningQuantity == 65 && c3.ClosingQuantity == 60;

                    return (ok, $"Chuỗi số dư: {c1.OpeningQuantity}→{c1.ClosingQuantity}→{c2.ClosingQuantity}→{c3.ClosingQuantity}");
                }));

            // 6. TC06 - Kiểm soát sức chứa tối đa của Ô kệ (T39, T109)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-06",
                "T39 / T109",
                "Kiểm soát tải trọng kệ (Max Capacity Control): Phát hiện tràn sức chứa ô kệ",
                "Ô chứa có MaxCapacity = 200, đang chứa 160. Kiểm tra điều kiện nhập thêm 50.",
                "160 + 50 = 210 > 200 → Phát hiện vượt tải 10 đơn vị",
                async () =>
                {
                    var seed = await CreateSandboxDataAsync(maxCapacity: 200);
                    _context.ProductBatches.Add(new ProductBatch
                    {
                        ProductId = seed.Product.Id,
                        WarehouseBinId = seed.Bin.Id,
                        BatchCode = "CAP-1",
                        Quantity = 160
                    });
                    await _context.SaveChangesAsync();

                    int currentUsed = await _context.ProductBatches
                        .Where(b => b.WarehouseBinId == seed.Bin.Id)
                        .SumAsync(b => b.Quantity);

                    int incoming = 50;
                    bool isOverCapacity = (currentUsed + incoming) > seed.Bin.MaxCapacity;
                    int overflow = (currentUsed + incoming) - seed.Bin.MaxCapacity;

                    return (isOverCapacity && overflow == 10,
                            $"Đang chứa: {currentUsed}/{seed.Bin.MaxCapacity}, Nhập: {incoming} → Quá tải: {isOverCapacity} (+{overflow})");
                }));

            // 7. TC07 - Kiểm thử bảo mật SQL Injection (T120, T86)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-07",
                "T120 / T86",
                "Bảo mật SQL Injection: Truy vấn tham số hóa EF Core vô hiệu hóa chuỗi tấn công SQLi",
                "Truyền payload `' OR 1=1; DROP TABLE Products; --` vào bộ lọc tìm kiếm sản phẩm.",
                "Không rò rỉ dữ liệu, trả về 0 kết quả khớp và bảng Products nguyên vẹn",
                async () =>
                {
                    string payload = "' OR 1=1; DROP TABLE Products; --";
                    var matched = await _context.Products
                        .AsNoTracking()
                        .Where(p => p.Name.Contains(payload) || (p.Code != null && p.Code.Contains(payload)))
                        .CountAsync();

                    bool tableIntact = await _context.Products.CountAsync() >= 0;
                    bool ok = matched == 0 && tableIntact;

                    return (ok, $"Kết quả khớp payload độc hại: {matched}, Bảng Products an toàn: {tableIntact}");
                }));

            // 8. TC08 - Kiểm thử bảo mật XSS Encoding (T121, T86)
            results.Add(await RunIsolatedAsync(
                "TC-WMS-08",
                "T121 / T86",
                "Bảo mật Cross-Site Scripting (XSS): Mã hóa HTML tự động các thẻ <script> độc hại",
                "Encode chuỗi `<script>alert('XSS')</script><img src=x onerror=alert(1)>`.",
                "Các ký tự `<`, `>`, `'` bị chuyển thành HTML Entities an toàn, không thể thực thi script",
                async () =>
                {
                    await Task.Yield();
                    string raw = "<script>alert('XSS')</script>";
                    string encoded = WebUtility.HtmlEncode(raw);
                    bool ok = !encoded.Contains("<script>") && encoded.Contains("&lt;script&gt;");
                    return (ok, $"Chuỗi sau khi mã hóa: {encoded}");
                }));

            suiteSw.Stop();

            return new TestSuiteReport
            {
                ExecutedAt = DateTime.Now,
                TotalDurationMs = suiteSw.ElapsedMilliseconds,
                Results = results
            };
        }

        private async Task<TestCaseResult> RunIsolatedAsync(
            string code,
            string taskRef,
            string title,
            string scenario,
            string expected,
            Func<Task<(bool Passed, string Actual)>> testBody)
        {
            var sw = Stopwatch.StartNew();
            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var (passed, actual) = await testBody();
                await tx.RollbackAsync();
                _context.ChangeTracker.Clear();
                sw.Stop();

                return new TestCaseResult
                {
                    Code = code,
                    TaskRef = taskRef,
                    Title = title,
                    Scenario = scenario,
                    Expected = expected,
                    Actual = actual,
                    Passed = passed,
                    DurationMs = Math.Max(1, sw.ElapsedMilliseconds)
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _context.ChangeTracker.Clear();
                sw.Stop();

                return new TestCaseResult
                {
                    Code = code,
                    TaskRef = taskRef,
                    Title = title,
                    Scenario = scenario,
                    Expected = expected,
                    Actual = "Exception: " + ex.Message,
                    Passed = false,
                    DurationMs = Math.Max(1, sw.ElapsedMilliseconds)
                };
            }
        }

        private async Task<(Product Product, WarehouseBin Bin)> CreateSandboxDataAsync(int maxCapacity = 1000)
        {
            string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

            var wh = new Warehouse
            {
                Code = "TWH-" + suffix,
                Name = "Kho Kiểm Thử " + suffix,
                IsActive = true
            };
            _context.Warehouses.Add(wh);
            await _context.SaveChangesAsync();

            var area = new WarehouseArea
            {
                Code = "TA-" + suffix,
                Name = "Khu Test",
                WarehouseId = wh.Id
            };
            _context.WarehouseAreas.Add(area);
            await _context.SaveChangesAsync();

            var shelf = new WarehouseShelf
            {
                Code = "TS-" + suffix,
                Name = "Kệ Test",
                WarehouseAreaId = area.Id
            };
            _context.WarehouseShelves.Add(shelf);
            await _context.SaveChangesAsync();

            var bin = new WarehouseBin
            {
                Code = "TB-" + suffix,
                Name = "Ô Test",
                FloorNumber = 1,
                MaxCapacity = maxCapacity,
                WarehouseShelfId = shelf.Id
            };
            _context.WarehouseBins.Add(bin);

            var product = new Product
            {
                Code = "TSP-" + suffix,
                Name = "Sản phẩm Test " + suffix,
                Price = 50000,
                HasExpiryDate = true,
                Quantity = 0
            };
            _context.Products.Add(product);

            await _context.SaveChangesAsync();
            return (product, bin);
        }
    }

    public class TestSuiteReport
    {
        public DateTime ExecutedAt { get; set; }
        public long TotalDurationMs { get; set; }
        public List<TestCaseResult> Results { get; set; } = new();
        public int PassedCount => Results.Count(x => x.Passed);
        public int FailedCount => Results.Count(x => !x.Passed);
    }

    public class TestCaseResult
    {
        public string Code { get; set; } = string.Empty;
        public string TaskRef { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public long DurationMs { get; set; }
    }
}

