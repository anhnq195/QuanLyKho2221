
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace QuanLyKho2221.Models
{
    public class WarehouseUserPermission
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public IdentityUser? User { get; set; }

        [Required]
        public int WarehouseId { get; set; }

        public Warehouse? Warehouse { get; set; }

        public bool CanView { get; set; } = true;

        public bool CanImport { get; set; } = false;

        public bool CanExport { get; set; } = false;

        public bool CanManage { get; set; } = false;
    }
}
