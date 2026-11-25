using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class AutoExportRequestViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn quy tắc xuất kho")]
        [Display(Name = "Quy tắc xuất kho")]
        public string Rule { get; set; } = "FEFO"; // FEFO, FIFO, LIFO

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng xuất phải lớn hơn 0")]
        [Display(Name = "Số lượng cần xuất")]
        public int Quantity { get; set; }

        [StringLength(150)]
        [Display(Name = "Người nhận")]
        public string? Receiver { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }
    }
}

