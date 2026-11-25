
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class InventoryCheckCreateViewModel
    {
        public string? Note { get; set; }

        public List<InventoryCheckItemViewModel> Items { get; set; }
            = new List<InventoryCheckItemViewModel>();
    }

    public class InventoryCheckItemViewModel
    {
        public int ProductId { get; set; }

        // Liên kết với lô hàng
        public int? ProductBatchId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? ProductCode { get; set; }

        public string BatchCode { get; set; } = string.Empty;

        public string BinLocation { get; set; } = string.Empty;

        public int SystemQuantity { get; set; }

        [Range(0, int.MaxValue,
            ErrorMessage = "Số lượng thực tế phải lớn hơn hoặc bằng 0")]
        public int ActualQuantity { get; set; }
    }
}
