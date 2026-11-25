
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class WarehousePermissionViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhân viên")]
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn kho")]
        public int WarehouseId { get; set; }

        public string WarehouseName { get; set; } = string.Empty;

        [Display(Name = "Quyền xem kho")]
        public bool CanView { get; set; } = true;

        [Display(Name = "Quyền nhập kho")]
        public bool CanImport { get; set; }

        [Display(Name = "Quyền xuất kho")]
        public bool CanExport { get; set; }

        [Display(Name = "Quyền quản lý kho")]
        public bool CanManage { get; set; }
    }
}
