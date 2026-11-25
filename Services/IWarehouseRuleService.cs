using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Services
{
    public class BatchAllocationItem
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = string.Empty;
        public int WarehouseBinId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CurrentStock { get; set; }
        public int PendingQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int AllocatedQuantity { get; set; }
    }

    public class OutboundAllocationPlan
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Rule { get; set; } = "FEFO"; // FEFO, FIFO, LIFO
        public int RequestedQuantity { get; set; }
        public int TotalAllocatedQuantity { get; set; }
        public bool IsSufficient => TotalAllocatedQuantity >= RequestedQuantity;
        public int ShortageQuantity => Math.Max(0, RequestedQuantity - TotalAllocatedQuantity);
        public List<BatchAllocationItem> Allocations { get; set; } = new();
    }

    public interface IWarehouseRuleService
    {
        /// <summary>
        /// Tính toán phân bổ lô hàng tự động theo quy tắc FEFO / FIFO / LIFO
        /// </summary>
        Task<OutboundAllocationPlan> CalculateOutboundAllocationAsync(
            int productId,
            int requestedQuantity,
            string rule = "FEFO");
    }
}

