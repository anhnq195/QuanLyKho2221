
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class Warehouse
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã kho")]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên kho")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? ManagerName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<WarehouseArea> Areas { get; set; }
            = new List<WarehouseArea>();
    }
}
