using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class WarehouseShelf
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã kệ")]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên kệ")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public int WarehouseAreaId { get; set; }

        public WarehouseArea? WarehouseArea { get; set; }

        public ICollection<WarehouseBin> Bins { get; set; }
            = new List<WarehouseBin>();
    }
}