using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyKho2221.Models
{
    public class StockCard
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Ngày giao dịch")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        [Display(Name = "Mã chứng từ")]
        public string ReceiptCode { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Loại giao dịch")]
        public string TransactionType { get; set; } = string.Empty; // Nhập kho, Xuất kho, Điều chuyển đi, Điều chuyển đến, Cân bằng kiểm kê

        [Required]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Display(Name = "Lô hàng")]
        public int? ProductBatchId { get; set; }

        public ProductBatch? ProductBatch { get; set; }

        [Display(Name = "Ô chứa")]
        public int? WarehouseBinId { get; set; }

        public WarehouseBin? WarehouseBin { get; set; }

        [Display(Name = "Tồn trước")]
        public int OpeningQuantity { get; set; }

        [Display(Name = "Biến động")]
        public int QuantityChange { get; set; } // Dương: tăng, Âm: giảm

        [Display(Name = "Tồn sau")]
        public int ClosingQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Đơn giá")]
        public decimal UnitPrice { get; set; } = 0m;

        [StringLength(500)]
        [Display(Name = "Ghi chú / Diễn giải")]
        public string? Note { get; set; }

        [StringLength(256)]
        [Display(Name = "Người thực hiện")]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

