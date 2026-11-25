using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Models;
using QuanLyKho2221.Services;

namespace QuanLyKho2221.Data
{
    /// <summary>
    /// T107 - Chuyển số dư tồn kho hiện có sang sổ Thẻ kho & Đảm bảo 100% sản phẩm đều có Mã sản phẩm (Code).
    /// </summary>
    public static class StockCardSeeder
    {
        public static async Task EnsureSupplierProductsAsync(WarehouseDbContext context)
        {
            // 1. Tự động khởi tạo bảng SupplierProducts trong SQL Server nếu chưa tồn tại
            const string createTableSql = @"
IF OBJECT_ID(N'[SupplierProducts]', N'U') IS NULL
BEGIN
    CREATE TABLE [SupplierProducts] (
        [Id] int NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [ProductId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_SupplierProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierProducts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_SupplierProducts_SupplierId_ProductId] ON [SupplierProducts] ([SupplierId], [ProductId]);
    CREATE INDEX [IX_SupplierProducts_ProductId] ON [SupplierProducts] ([ProductId]);
END";
            await context.Database.ExecuteSqlRawAsync(createTableSql);

            // 2. Đồng bộ tự động các cặp (Nhà cung cấp - Sản phẩm) từ lịch sử phiếu nhập nếu chưa có
            bool anyMapping = await context.SupplierProducts.AnyAsync();
            if (!anyMapping)
            {
                var historicalPairs = await context.ImportReceipts
                    .AsNoTracking()
                    .Select(r => new { r.SupplierId, r.ProductId })
                    .Distinct()
                    .ToListAsync();

                foreach (var pair in historicalPairs)
                {
                    context.SupplierProducts.Add(new SupplierProduct
                    {
                        SupplierId = pair.SupplierId,
                        ProductId = pair.ProductId,
                        CreatedAt = DateTime.Now
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        public static async Task EnsureProductCodesAsync(WarehouseDbContext context)
        {
            var missingCodeProducts = await context.Products
                .Where(p => p.Code == null || p.Code == "")
                .ToListAsync();

            if (missingCodeProducts.Count == 0)
                return;

            var existingCodes = new HashSet<string>(
                await context.Products
                    .Where(p => p.Code != null && p.Code != "")
                    .Select(p => p.Code!)
                    .ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            foreach (var prod in missingCodeProducts)
            {
                string candidate;
                do
                {
                    char[] buf = new char[6];
                    for (int i = 0; i < buf.Length; i++)
                        buf[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
                    candidate = "SP-" + new string(buf);
                }
                while (!existingCodes.Add(candidate));

                prod.Code = candidate;
            }

            await context.SaveChangesAsync();
        }

        public static async Task SeedOpeningBalancesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();

            // 1. Đảm bảo mọi sản phẩm đều có Mã sản phẩm (SKU Code) và bảng SupplierProducts sẵn sàng
            await EnsureProductCodesAsync(context);
            await EnsureSupplierProductsAsync(context);

            // 2. Kết chuyển số dư đầu kỳ sang sổ Thẻ kho
            var productIdsWithCards = await context.StockCards
                .Select(s => s.ProductId)
                .Distinct()
                .ToListAsync();

            var products = await context.Products
                .Where(p => !productIdsWithCards.Contains(p.Id))
                .ToListAsync();

            if (products.Count == 0)
                return;

            var productIds = products.Select(p => p.Id).ToList();

            var batches = await context.ProductBatches
                .Where(b => productIds.Contains(b.ProductId) && b.Quantity > 0)
                .ToListAsync();

            var now = DateTime.Now;
            var batchesByProduct = batches.ToLookup(b => b.ProductId);

            foreach (var product in products)
            {
                var productBatches = batchesByProduct[product.Id].ToList();
                int running = 0;

                if (productBatches.Count > 0)
                {
                    foreach (var batch in productBatches.OrderBy(b => b.Id))
                    {
                        context.StockCards.Add(new StockCard
                        {
                            TransactionDate = now,
                            ReceiptCode = "SDDK",
                            TransactionType = StockCardService.OpeningBalanceType,
                            ProductId = product.Id,
                            ProductBatchId = batch.Id,
                            WarehouseBinId = batch.WarehouseBinId,
                            OpeningQuantity = running,
                            QuantityChange = batch.Quantity,
                            ClosingQuantity = running + batch.Quantity,
                            Note = $"Chuyển số dư lô {batch.BatchCode} sang sổ thẻ kho",
                            CreatedBy = "Hệ thống",
                            CreatedAt = now
                        });
                        running += batch.Quantity;
                    }
                }
                else if (product.Quantity > 0)
                {
                    context.StockCards.Add(new StockCard
                    {
                        TransactionDate = now,
                        ReceiptCode = "SDDK",
                        TransactionType = StockCardService.OpeningBalanceType,
                        ProductId = product.Id,
                        OpeningQuantity = 0,
                        QuantityChange = product.Quantity,
                        ClosingQuantity = product.Quantity,
                        Note = "Chuyển số dư sản phẩm chưa phân lô sang sổ thẻ kho",
                        CreatedBy = "Hệ thống",
                        CreatedAt = now
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
