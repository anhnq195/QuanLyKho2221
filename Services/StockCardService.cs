using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Services
{
    public class StockCardService : IStockCardService
    {
        public const string OpeningBalanceType = "Số dư đầu kỳ";

        private readonly WarehouseDbContext _context;

        public StockCardService(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<StockCard> RecordEntryAsync(
            string receiptCode,
            string transactionType,
            int productId,
            int? productBatchId,
            int? warehouseBinId,
            int quantityChange,
            decimal unitPrice = 0m,
            string? note = null,
            string? createdBy = null,
            DateTime? transactionDate = null)
        {
            // Tồn trước = tổng lũy kế của sổ thẻ kho (không phụ thuộc vào việc
            // controller đã cập nhật Product.Quantity trước hay sau khi gọi hàm này).
            int openingQuantity = await _context.StockCards
                .Where(s => s.ProductId == productId)
                .SumAsync(s => (int?)s.QuantityChange) ?? 0;

            var card = new StockCard
            {
                TransactionDate = transactionDate ?? DateTime.Now,
                ReceiptCode = receiptCode,
                TransactionType = transactionType,
                ProductId = productId,
                ProductBatchId = productBatchId,
                WarehouseBinId = warehouseBinId,
                OpeningQuantity = openingQuantity,
                QuantityChange = quantityChange,
                ClosingQuantity = openingQuantity + quantityChange,
                UnitPrice = unitPrice,
                Note = note,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            };

            _context.StockCards.Add(card);
            await _context.SaveChangesAsync();

            return card;
        }
    }
}
