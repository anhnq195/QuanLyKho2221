using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;

namespace QuanLyKho2221.Services
{
    public interface IWarehousePermissionService
    {
        Task<(bool Allowed, string? DenyReason)> CheckBinPermissionAsync(
            ClaimsPrincipal user,
            int warehouseBinId,
            string operation); // "View", "Import", "Export", "Manage"
    }

    /// <summary>
    /// T61 (RQ61 / PB09) - Kiểm tra phân quyền chi tiết của nhân viên trên từng kho và thao tác.
    /// </summary>
    public class WarehousePermissionService : IWarehousePermissionService
    {
        private readonly WarehouseDbContext _context;

        public WarehousePermissionService(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Allowed, string? DenyReason)> CheckBinPermissionAsync(
            ClaimsPrincipal user,
            int warehouseBinId,
            string operation)
        {
            if (user.IsInRole("Admin"))
                return (true, null);

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return (false, "Không xác định được tài khoản người dùng.");

            // Nếu nhân viên chưa được gán bất kỳ cấu hình phân quyền kho nào thì mặc định cho phép tác nghiệp chung.
            // Khi Admin đã cấu hình phân quyền cho nhân viên trong bảng WarehouseUserPermissions, hệ thống kiểm tra nghiêm ngặt.
            bool hasAnyRules = await _context.WarehouseUserPermissions
                .AsNoTracking()
                .AnyAsync(p => p.UserId == userId);

            if (!hasAnyRules)
                return (true, null);

            var bin = await _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                        .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == warehouseBinId);

            var warehouse = bin?.WarehouseShelf?.WarehouseArea?.Warehouse;
            if (warehouse == null)
                return (false, "Không xác định được kho của ô chứa này.");

            var perm = await _context.WarehouseUserPermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WarehouseId == warehouse.Id);

            if (perm == null)
            {
                return (false, $"Tài khoản của bạn chưa được cấp quyền truy cập tại kho \"{warehouse.Name}\".");
            }

            bool allowed = operation switch
            {
                "Import" => perm.CanImport || perm.CanManage,
                "Export" => perm.CanExport || perm.CanManage,
                "Manage" => perm.CanManage,
                _ => perm.CanView || perm.CanImport || perm.CanExport || perm.CanManage
            };

            if (!allowed)
            {
                string opName = operation switch
                {
                    "Import" => "Nhập kho",
                    "Export" => "Xuất kho",
                    "Manage" => "Quản lý / Điều chuyển",
                    _ => "Xem"
                };
                return (false, $"Bạn không có quyền \"{opName}\" tại kho \"{warehouse.Name}\". Vui lòng liên hệ Admin.");
            }

            return (true, null);
        }
    }
}
