using System;
using System.Threading.Tasks;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Services
{
    public interface IStockCardService
    {
        Task<StockCard> RecordEntryAsync(
            string receiptCode,
            string transactionType,
            int productId,
            int? productBatchId,
            int? warehouseBinId,
            int quantityChange,
            decimal unitPrice = 0m,
            string? note = null,
            string? createdBy = null,
            DateTime? transactionDate = null);
    }
}

