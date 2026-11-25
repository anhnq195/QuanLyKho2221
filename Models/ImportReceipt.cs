
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyKho2221.Models
{
    public class ImportReceipt
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Mã phiếu nhập không được để trống")]
        [StringLength(50)]
        [Display(Name = "Mã phiếu nhập")]
        public string ReceiptCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [Display(Name = "Nhà cung cấp")]
        public int SupplierId { get; set; }

        public Supplier? Supplier { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        public int? ProductBatchId { get; set; }

        public ProductBatch? ProductBatch { get; set; }

        [Display(Name = "Ô chứa")]
        public int? WarehouseBinId { get; set; }

        public WarehouseBin? WarehouseBin { get; set; }

        [StringLength(50)]
        [Display(Name = "Mã lô hàng")]
        public string? BatchCode { get; set; }

        [Display(Name = "Ngày sản xuất")]
        public DateTime? ManufactureDate { get; set; }

        [Display(Name = "Hạn sử dụng")]
        public DateTime? ExpiryDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        [Display(Name = "Số lượng nhập")]
        public int Quantity { get; set; }

        // Giữ lại để tương thích với cột hiện tại trong database.
        // Không sử dụng đơn giá trên giao diện nhập kho.
        public decimal UnitPrice { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

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
