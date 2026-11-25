
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ActivityLogsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public ActivityLogsController(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? keyword,
            string? userName,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _context.ActivityLogs
                .AsNoTracking()
                .AsQueryable();

            // TÌM KIẾM
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(x =>
                    x.UserName.Contains(keyword) ||
                    x.Action.Contains(keyword) ||
                    (x.Description != null &&
                     x.Description.Contains(keyword)));
            }

            // LỌC TÀI KHOẢN
            if (!string.IsNullOrWhiteSpace(userName))
            {
                query = query.Where(x => x.UserName == userName);
            }

            // LỌC TỪ NGÀY
            if (fromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= fromDate.Value.Date);
            }

            // LỌC ĐẾN NGÀY
            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);

                query = query.Where(x =>
                    x.CreatedAt < endDate);
            }

            // THỐNG KÊ TỔNG QUÁT
            ViewBag.TotalCount = await query.CountAsync();

            // NHẬP XUẤT KHO
            ViewBag.ImportCount = await query.CountAsync(
                x => x.Action == "Nhập kho" || x.Action == "Duyệt phiếu nhập");

            ViewBag.ExportCount = await query.CountAsync(
                x => x.Action == "Xuất kho" || x.Action == "Duyệt phiếu xuất");

            // ĐIỀU CHUYỂN KHO
            ViewBag.TransferCount = await query.CountAsync(
                x => x.Action == "Điều chuyển kho");

            // ĐĂNG NHẬP, ĐĂNG XUẤT
            ViewBag.LoginCount = await query.CountAsync(
                x => x.Action == "Đăng nhập");

            ViewBag.LogoutCount = await query.CountAsync(
                x => x.Action == "Đăng xuất");

            // QUẢN LÝ SẢN PHẨM
            ViewBag.AddProductCount = await query.CountAsync(
                x => x.Action == "Thêm sản phẩm");

            ViewBag.EditProductCount = await query.CountAsync(
                x => x.Action == "Sửa sản phẩm");

            ViewBag.DeleteProductCount = await query.CountAsync(
                x => x.Action == "Xóa sản phẩm");

            // QUẢN LÝ DANH MỤC
            ViewBag.AddCategoryCount = await query.CountAsync(
                x => x.Action == "Thêm danh mục");

            ViewBag.EditCategoryCount = await query.CountAsync(
                x => x.Action == "Sửa danh mục");

            ViewBag.DeleteCategoryCount = await query.CountAsync(
                x => x.Action == "Xóa danh mục");

            // QUẢN LÝ NHÀ CUNG CẤP
            ViewBag.AddSupplierCount = await query.CountAsync(
                x => x.Action == "Thêm nhà cung cấp");

            ViewBag.EditSupplierCount = await query.CountAsync(
                x => x.Action == "Sửa nhà cung cấp");

            ViewBag.DeleteSupplierCount = await query.CountAsync(
                x => x.Action == "Xóa nhà cung cấp");

            // KIỂM KÊ KHO
            ViewBag.CreateInventoryCheckCount = await query.CountAsync(
                x => x.Action == "Tạo phiếu kiểm kê");

            ViewBag.ConfirmInventoryCheckCount = await query.CountAsync(
                x => x.Action == "Xác nhận kiểm kê");

            // DANH SÁCH TÀI KHOẢN
            ViewBag.Users = await _context.ActivityLogs
                .AsNoTracking()
                .Select(x => x.UserName)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            // GIỮ BỘ LỌC
            ViewBag.Keyword = keyword;
            ViewBag.UserName = userName;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            // DANH SÁCH NHẬT KÝ
            var logs = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(logs);
        }
    }
}
