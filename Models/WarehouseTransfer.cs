
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class WarehouseTransfer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string TransferCode { get; set; } = string.Empty;

        public DateTime TransferDate { get; set; } = DateTime.Now;

        public int ProductBatchId { get; set; }

        public ProductBatch? ProductBatch { get; set; }

        public int SourceBinId { get; set; }

        public WarehouseBin? SourceBin { get; set; }

        public int DestinationBinId { get; set; }

        public WarehouseBin? DestinationBin { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Số lượng điều chuyển phải lớn hơn 0")]
        public int Quantity { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
    }
}
