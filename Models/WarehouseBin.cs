
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class WarehouseBin
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã ô chứa")]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên ô chứa")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "Tầng phải từ 1 đến 100")]
        [Display(Name = "Tầng")]
        public int FloorNumber { get; set; } = 1;

        [Range(1, 1000000, ErrorMessage = "Sức chứa tối đa phải lớn hơn 0")]
        [Display(Name = "Sức chứa tối đa (số lượng)")]
        public int MaxCapacity { get; set; } = 1000;

        public int WarehouseShelfId { get; set; }

        public WarehouseShelf? WarehouseShelf { get; set; }

        public ICollection<ProductBatch> ProductBatches { get; set; }
            = new List<ProductBatch>();
    }
}
