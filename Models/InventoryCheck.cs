
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class InventoryCheck
    {
        public int Id { get; set; }

        [Required]
        public string CheckCode { get; set; } = string.Empty;

        public DateTime CheckDate { get; set; } = DateTime.Now;

        public string? Note { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public bool IsConfirmed { get; set; } = false;

        public List<InventoryCheckDetail> Details { get; set; }
            = new List<InventoryCheckDetail>();
    }
}
