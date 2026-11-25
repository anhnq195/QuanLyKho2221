using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class WarehouseArea
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã khu vực")]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên khu vực")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public int WarehouseId { get; set; }

        public Warehouse? Warehouse { get; set; }

        public ICollection<WarehouseShelf> Shelves { get; set; }
            = new List<WarehouseShelf>();
    }
}