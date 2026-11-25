using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(150, ErrorMessage = "Tên sản phẩm không được vượt quá 150 ký tự")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Mã sản phẩm không được vượt quá 50 ký tự")]
        [Display(Name = "Mã sản phẩm")]
        public string? Code { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng không được nhỏ hơn 0")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0", "999999999999",
            ErrorMessage = "Giá sản phẩm không được nhỏ hơn 0")]
        [Display(Name = "Đơn giá")]
        public decimal Price { get; set; }

        [Display(Name = "Có hạn sử dụng")]
        public bool HasExpiryDate { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CategoryId { get; set; }

        public Category? Category { get; set; }

        public ICollection<ProductBatch> Batches { get; set; }
            = new List<ProductBatch>();

        public ICollection<SupplierProduct> SupplierProducts { get; set; }
            = new List<SupplierProduct>();
    }
}