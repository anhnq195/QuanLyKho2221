
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyKho2221.Models
{
    public class ExportReceipt
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Mã phiếu xuất")]
        public string ReceiptCode { get; set; } = string.Empty;

        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "Lô hàng")]
        public int? ProductBatchId { get; set; }
        public ProductBatch? ProductBatch { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Số lượng xuất phải lớn hơn 0")]
        [Display(Name = "Số lượng xuất")]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999",
            ErrorMessage = "Đơn giá phải lớn hơn 0")]
        [Display(Name = "Đơn giá xuất")]
        public decimal UnitPrice { get; set; }

        [StringLength(150)]
        [Display(Name = "Người nhận")]
        public string? Receiver { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = ReceiptStatus.Pending;

        [StringLength(256)]
        public string? CreatedBy { get; set; }

        [StringLength(256)]
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(256)]
        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        [StringLength(256)]
        public string? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }

        [NotMapped]
        public decimal TotalAmount => Quantity * UnitPrice;
    }
}
