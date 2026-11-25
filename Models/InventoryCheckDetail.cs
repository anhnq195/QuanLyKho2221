
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class InventoryCheckDetail
    {
        public int Id { get; set; }

        public int InventoryCheckId { get; set; }

        public InventoryCheck? InventoryCheck { get; set; }

        public int ProductId { get; set; }

        public Product? Product { get; set; }

        // Liên kết với lô hàng
        public int? ProductBatchId { get; set; }

        public ProductBatch? ProductBatch { get; set; }

        public int SystemQuantity { get; set; }

        [Range(0, int.MaxValue,
            ErrorMessage = "Số lượng thực tế không hợp lệ")]
        public int ActualQuantity { get; set; }

        public int Difference =>
            ActualQuantity - SystemQuantity;
    }
}
