
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly WarehouseDbContext _context;

        public InventoryController(WarehouseDbContext context)
        {
            _context = context;
        }

        // LỌC VÀ BÁO CÁO TỒN KHO
        public async Task<IActionResult> Index(
            string? keyword,
            string? stockStatus,
            string? sortOrder)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    (p.Code != null && p.Code.Contains(keyword)));
            }

            query = ApplyStockFilter(query, stockStatus);
            query = ApplySort(query, sortOrder);

            var products = await query.ToListAsync();

            // THỐNG KÊ TỔNG KHO
            ViewBag.TotalProducts = await _context.Products.CountAsync();

            ViewBag.TotalQuantity = await _context.Products
                .SumAsync(p => (int?)p.Quantity) ?? 0;

            ViewBag.TotalValue = await _context.Products
                .SumAsync(p => (decimal?)(p.Quantity * p.Price)) ?? 0m;

            ViewBag.LowStock = await _context.Products.CountAsync(
                p => p.Quantity >= 1 && p.Quantity <= 5);

            ViewBag.OutOfStock = await _context.Products.CountAsync(
                p => p.Quantity <= 0);

            ViewBag.Keyword = keyword;
            ViewBag.StockStatus = stockStatus;
            ViewBag.SortOrder = sortOrder;

            return View(products);
        }

        // LỊCH SỬ NHẬP XUẤT CỦA SẢN PHẨM
        public async Task<IActionResult> History(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            var model = new ProductHistoryViewModel
            {
                Product = product,

                Imports = await _context.ImportReceipts
                    .Include(x => x.Supplier)
                    .Where(x => x.ProductId == id && x.Status == ReceiptStatus.Approved)
                    .OrderByDescending(x => x.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync(),

                Exports = await _context.ExportReceipts
                    .Where(x => x.ProductId == id && x.Status == ReceiptStatus.Approved)
                    .OrderByDescending(x => x.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync()
            };

            return View(model);
        }

        // XUẤT EXCEL THEO BỘ LỌC
        public async Task<IActionResult> ExportExcel(
            string? keyword,
            string? stockStatus,
            string? sortOrder)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    (p.Code != null && p.Code.Contains(keyword)));
            }

            query = ApplyStockFilter(query, stockStatus);
            query = ApplySort(query, sortOrder);

            var products = await query.ToListAsync();

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Bao cao ton kho");

            // TIÊU ĐỀ
            worksheet.Cell(1, 1).Value = "BÁO CÁO TỒN KHO";
            worksheet.Range(1, 1, 1, 7).Merge();

            var title = worksheet.Range(1, 1, 1, 7);
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 18;
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            title.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 32;

            // THÔNG TIN BÁO CÁO
            worksheet.Cell(2, 1).Value = "Ngày xuất báo cáo:";
            worksheet.Cell(2, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            worksheet.Cell(3, 1).Value = "Từ khóa:";
            worksheet.Cell(3, 2).Value =
                string.IsNullOrWhiteSpace(keyword) ? "Tất cả" : keyword;

            worksheet.Cell(4, 1).Value = "Trạng thái:";
            worksheet.Cell(4, 2).Value = stockStatus switch
            {
                "low" => "Sắp hết hàng",
                "out" => "Hết hàng",
                "available" => "Còn hàng",
                _ => "Tất cả"
            };

            // TIÊU ĐỀ BẢNG
            string[] headers =
            {
                "Mã sản phẩm",
                "Tên sản phẩm",
                "Danh mục",
                "Số lượng tồn",
                "Đơn giá (VNĐ)",
                "Giá trị tồn kho (VNĐ)",
                "Tình trạng"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(6, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(6, 1, 6, 7);

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D6EFD");
            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(6).Height = 25;

            // DỮ LIỆU SẢN PHẨM
            int row = 7;

            foreach (var item in products)
            {
                worksheet.Cell(row, 1).Value = item.Code ?? "";
                worksheet.Cell(row, 2).Value = item.Name;
                worksheet.Cell(row, 3).Value =
                    item.Category?.Name ?? "Chưa phân loại";

                worksheet.Cell(row, 4).Value = item.Quantity;
                worksheet.Cell(row, 5).Value = item.Price;
                worksheet.Cell(row, 6).Value =
                    item.Quantity * item.Price;

                string status;

                if (item.Quantity <= 0)
                {
                    status = "Hết hàng";
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                }
                else if (item.Quantity <= 5)
                {
                    status = "Sắp hết hàng";
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Orange;
                }
                else
                {
                    status = "Còn hàng";
                    worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Green;
                }

                worksheet.Cell(row, 7).Value = status;

                // ĐỊNH DẠNG TIỀN
                worksheet.Cell(row, 5).Style.NumberFormat.Format =
                    "#,##0";

                worksheet.Cell(row, 6).Style.NumberFormat.Format =
                    "#,##0";

                // KẺ VIỀN
                worksheet.Range(row, 1, row, 7).Style.Border
                    .InsideBorder = XLBorderStyleValues.Thin;

                worksheet.Range(row, 1, row, 7).Style.Border
                    .OutsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            // DÒNG TỔNG KẾT
            int summaryRow = row + 1;

            worksheet.Cell(summaryRow, 1).Value = "TỔNG CỘNG";
            worksheet.Range(summaryRow, 1, summaryRow, 3).Merge();

            worksheet.Cell(summaryRow, 4).FormulaA1 =
                $"SUM(D7:D{row - 1})";

            worksheet.Cell(summaryRow, 6).FormulaA1 =
                $"SUM(F7:F{row - 1})";

            var summaryRange = worksheet.Range(
                summaryRow, 1, summaryRow, 7);

            summaryRange.Style.Font.Bold = true;
            summaryRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

            worksheet.Cell(summaryRow, 6).Style.NumberFormat.Format =
                "#,##0";

            // BẬT BỘ LỌC
            if (products.Count > 0)
            {
                worksheet.Range(6, 1, row - 1, 7).SetAutoFilter();
            }

            // CĂN CHỈNH CỘT
            worksheet.Columns().AdjustToContents();

            worksheet.Column(2).Width =
                Math.Max(worksheet.Column(2).Width, 25);

            worksheet.Column(6).Width =
                Math.Max(worksheet.Column(6).Width, 23);

            worksheet.SheetView.FreezeRows(6);

            // LƯU FILE
            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        // LỌC TRẠNG THÁI TỒN KHO
        private IQueryable<Product> ApplyStockFilter(
            IQueryable<Product> query,
            string? stockStatus)
        {
            if (stockStatus == "low")
            {
                query = query.Where(p =>
                    p.Quantity >= 1 && p.Quantity <= 5);
            }
            else if (stockStatus == "out")
            {
                query = query.Where(p => p.Quantity <= 0);
            }
            else if (stockStatus == "available")
            {
                query = query.Where(p => p.Quantity > 5);
            }

            return query;
        }

        // SẮP XẾP SẢN PHẨM
        private IQueryable<Product> ApplySort(
            IQueryable<Product> query,
            string? sortOrder)
        {
            return sortOrder switch
            {
                "quantity_asc" => query.OrderBy(p => p.Quantity),
                "quantity_desc" => query.OrderByDescending(p => p.Quantity),
                "name_desc" => query.OrderByDescending(p => p.Name),
                _ => query.OrderBy(p => p.Name)
            };
        }
    }
}
