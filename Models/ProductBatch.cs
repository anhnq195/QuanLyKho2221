
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class ProductBatch
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ô chứa")]
        public int WarehouseBinId { get; set; }

        public WarehouseBin? WarehouseBin { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã lô hàng")]
        [StringLength(50, ErrorMessage = "Mã lô không được vượt quá 50 ký tự")]
        [Display(Name = "Mã lô hàng")]
        public string BatchCode { get; set; } = string.Empty;

        [Display(Name = "Ngày sản xuất")]
        public DateTime? ManufactureDate { get; set; }

        [Display(Name = "Hạn sử dụng")]
        public DateTime? ExpiryDate { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng không được âm")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
