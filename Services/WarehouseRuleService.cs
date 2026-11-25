using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Services
{
    public class WarehouseRuleService : IWarehouseRuleService
    {
        private readonly WarehouseDbContext _context;

        public WarehouseRuleService(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<OutboundAllocationPlan> CalculateOutboundAllocationAsync(
            int productId,
            int requestedQuantity,
            string rule = "FEFO")
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            var plan = new OutboundAllocationPlan
            {
                ProductId = productId,
                ProductName = product?.Name ?? "Sản phẩm",
                RequestedQuantity = requestedQuantity,
                Rule = rule.ToUpperInvariant()
            };

            if (product == null || requestedQuantity <= 0)
            {
                return plan;
            }

            // Tải danh sách các lô hàng còn tồn của sản phẩm
            var batches = await _context.ProductBatches
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(shelf => shelf!.WarehouseArea)
                            .ThenInclude(area => area!.Warehouse)
                .Where(b => b.ProductId == productId && b.Quantity > 0)
                .AsNoTracking()
                .ToListAsync();

            if (!batches.Any())
            {
                return plan;
            }

            // Lấy số lượng đang giữ chỗ (Pending) theo từng lô
            var pendingByBatch = await _context.ExportReceipts
                .AsNoTracking()
                .Where(x => x.Status == ReceiptStatus.Pending && x.ProductBatchId.HasValue && x.ProductId == productId)
                .GroupBy(x => x.ProductBatchId!.Value)
                .Select(g => new
                {
                    BatchId = g.Key,
                    PendingQuantity = g.Sum(x => (int)x.Quantity)
                })
                .ToDictionaryAsync(x => x.BatchId, x => x.PendingQuantity);

            // Kiểm tra các lô có phiếu nhập được duyệt (tránh xuất lô chưa duyệt nhập)
            var batchIds = batches.Select(b => b.Id).ToList();
            var unapprovedImportBatchIds = await _context.ImportReceipts
                .AsNoTracking()
                .Where(x => x.ProductBatchId.HasValue && batchIds.Contains(x.ProductBatchId.Value) && x.Status != ReceiptStatus.Approved)
                .Select(x => x.ProductBatchId!.Value)
                .Distinct()
                .ToListAsync();

            // Tính khả dụng từng lô
            var availableBatches = new List<(ProductBatch Batch, int Available, int Pending)>();
            foreach (var b in batches)
            {
                if (unapprovedImportBatchIds.Contains(b.Id))
                {
                    // Lô này có phiếu nhập chưa duyệt, bỏ qua
                    continue;
                }

                pendingByBatch.TryGetValue(b.Id, out int pending);
                int available = b.Quantity - pending;

                if (available > 0)
                {
                    availableBatches.Add((b, available, pending));
                }
            }

            // SẮP XẾP THEO QUY TẮC
            IEnumerable<(ProductBatch Batch, int Available, int Pending)> sortedBatches;

            switch (plan.Rule)
            {
                case "FEFO": // First Expired, First Out
                    // Lọc bỏ hàng đã quá hạn
                    sortedBatches = availableBatches
                        .Where(x => !x.Batch.ExpiryDate.HasValue || x.Batch.ExpiryDate.Value.Date >= DateTime.Today)
                        .OrderBy(x => x.Batch.ExpiryDate.HasValue ? 0 : 1) // Có HSD ưu tiên trước
                        .ThenBy(x => x.Batch.ExpiryDate) // HSD gần nhất xuất trước
                        .ThenBy(x => x.Batch.CreatedAt);
                    break;

                case "LIFO": // Last In, First Out
                    sortedBatches = availableBatches
                        .OrderByDescending(x => x.Batch.CreatedAt)
                        .ThenByDescending(x => x.Batch.Id);
                    break;

                case "FIFO": // First In, First Out
                default:
                    plan.Rule = "FIFO";
                    sortedBatches = availableBatches
                        .OrderBy(x => x.Batch.CreatedAt)
                        .ThenBy(x => x.Batch.Id);
                    break;
            }

            // Phân bổ số lượng
            int remaining = requestedQuantity;

            foreach (var item in sortedBatches)
            {
                if (remaining <= 0) break;

                int allocate = Math.Min(remaining, item.Available);

                var bin = item.Batch.WarehouseBin;
                var shelf = bin?.WarehouseShelf;
                var area = shelf?.WarehouseArea;
                var warehouse = area?.Warehouse;

                string locationText = $"{warehouse?.Name ?? "Kho"} > {area?.Name ?? "Khu"} > {shelf?.Name ?? "Kệ"} > Ô {bin?.Code ?? "N/A"}";

                plan.Allocations.Add(new BatchAllocationItem
                {
                    BatchId = item.Batch.Id,
                    BatchCode = item.Batch.BatchCode,
                    WarehouseBinId = item.Batch.WarehouseBinId,
                    LocationName = locationText,
                    ManufactureDate = item.Batch.ManufactureDate,
                    ExpiryDate = item.Batch.ExpiryDate,
                    CreatedAt = item.Batch.CreatedAt,
                    CurrentStock = item.Batch.Quantity,
                    PendingQuantity = item.Pending,
                    AvailableQuantity = item.Available,
                    AllocatedQuantity = allocate
                });

                remaining -= allocate;
                plan.TotalAllocatedQuantity += allocate;
            }

            return plan;
        }
    }
}

