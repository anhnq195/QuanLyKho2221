using QuanLyKho2221.Models;

namespace QuanLyKho2221.Models
{
    public class ProductHistoryViewModel
    {
        public Product Product { get; set; } = new();

        public List<ImportReceipt> Imports { get; set; } = new();

        public List<ExportReceipt> Exports { get; set; } = new();
    }
}