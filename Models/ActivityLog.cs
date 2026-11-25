
using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        [Required]
        public string UserName { get; set; } = "";

        [Required]
        public string Action { get; set; } = "";

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
