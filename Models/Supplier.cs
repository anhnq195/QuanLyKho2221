
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nhà cung cấp")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<SupplierProduct> SupplierProducts { get; set; }
            = new List<SupplierProduct>();
    }
}
