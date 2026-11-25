using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Data
{
    public class WarehouseDbContext
        : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public WarehouseDbContext(
            DbContextOptions<WarehouseDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        public DbSet<ImportReceipt> ImportReceipts { get; set; }
        public DbSet<ExportReceipt> ExportReceipts { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        public DbSet<InventoryCheck> InventoryChecks { get; set; }
        public DbSet<InventoryCheckDetail> InventoryCheckDetails { get; set; }

        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<WarehouseArea> WarehouseAreas { get; set; }
        public DbSet<WarehouseShelf> WarehouseShelves { get; set; }
        public DbSet<WarehouseBin> WarehouseBins { get; set; }

        public DbSet<ProductBatch> ProductBatches { get; set; }
        public DbSet<WarehouseTransfer> WarehouseTransfers { get; set; }
        public DbSet<WarehouseUserPermission> WarehouseUserPermissions { get; set; }
        public DbSet<StockCard> StockCards { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // PRODUCT - CATEGORY
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            // IMPORT RECEIPT
            modelBuilder.Entity<ImportReceipt>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ImportReceipt>()
                .Property(p => p.Status)
                .HasDefaultValue(ReceiptStatus.Approved);

            // IMPORT RECEIPT - WAREHOUSE BIN
            modelBuilder.Entity<ImportReceipt>()
                .HasOne(r => r.WarehouseBin)
                .WithMany()
                .HasForeignKey(r => r.WarehouseBinId)
                .OnDelete(DeleteBehavior.Restrict);

            // EXPORT RECEIPT
            modelBuilder.Entity<ExportReceipt>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ExportReceipt>()
                .Property(p => p.Status)
                .HasDefaultValue(ReceiptStatus.Approved);

            // INVENTORY CHECK
            modelBuilder.Entity<InventoryCheck>()
                .HasMany(c => c.Details)
                .WithOne(d => d.InventoryCheck)
                .HasForeignKey(d => d.InventoryCheckId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryCheckDetail>()
                .HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryCheckDetail>()
                .HasOne(d => d.ProductBatch)
                .WithMany()
                .HasForeignKey(d => d.ProductBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // WAREHOUSE - AREA
            modelBuilder.Entity<Warehouse>()
                .HasMany(w => w.Areas)
                .WithOne(a => a.Warehouse)
                .HasForeignKey(a => a.WarehouseId)
                .OnDelete(DeleteBehavior.Cascade);

            // AREA - SHELF
            modelBuilder.Entity<WarehouseArea>()
                .HasMany(a => a.Shelves)
                .WithOne(s => s.WarehouseArea)
                .HasForeignKey(s => s.WarehouseAreaId)
                .OnDelete(DeleteBehavior.Cascade);

            // SHELF - BIN
            modelBuilder.Entity<WarehouseShelf>()
                .HasMany(s => s.Bins)
                .WithOne(b => b.WarehouseShelf)
                .HasForeignKey(b => b.WarehouseShelfId)
                .OnDelete(DeleteBehavior.Cascade);

            // BIN - PRODUCT BATCH
            modelBuilder.Entity<WarehouseBin>()
                .HasMany(b => b.ProductBatches)
                .WithOne(p => p.WarehouseBin)
                .HasForeignKey(p => p.WarehouseBinId)
                .OnDelete(DeleteBehavior.Restrict);

            // WAREHOUSE BIN MAX CAPACITY
            modelBuilder.Entity<WarehouseBin>()
                .Property(b => b.MaxCapacity)
                .HasDefaultValue(1000);

            // PRODUCT - PRODUCT BATCH
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Batches)
                .WithOne(b => b.Product)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // WAREHOUSE TRANSFER - PRODUCT BATCH
            modelBuilder.Entity<WarehouseTransfer>()
                .HasOne(t => t.ProductBatch)
                .WithMany()
                .HasForeignKey(t => t.ProductBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // WAREHOUSE TRANSFER - SOURCE BIN
            modelBuilder.Entity<WarehouseTransfer>()
                .HasOne(t => t.SourceBin)
                .WithMany()
                .HasForeignKey(t => t.SourceBinId)
                .OnDelete(DeleteBehavior.Restrict);

            // WAREHOUSE TRANSFER - DESTINATION BIN
            modelBuilder.Entity<WarehouseTransfer>()
                .HasOne(t => t.DestinationBin)
                .WithMany()
                .HasForeignKey(t => t.DestinationBinId)
                .OnDelete(DeleteBehavior.Restrict);

            // UNIQUE WAREHOUSE CODE
            modelBuilder.Entity<Warehouse>()
                .HasIndex(w => w.Code)
                .IsUnique();

            // PRODUCT BATCH INDEX
            modelBuilder.Entity<ProductBatch>()
                .HasIndex(b => new
                {
                    b.ProductId,
                    b.BatchCode,
                    b.WarehouseBinId
                });

            // WAREHOUSE USER PERMISSION
            modelBuilder.Entity<WarehouseUserPermission>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseUserPermission>()
                .HasOne(p => p.Warehouse)
                .WithMany()
                .HasForeignKey(p => p.WarehouseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseUserPermission>()
                .HasIndex(p => new { p.UserId, p.WarehouseId })
                .IsUnique();

            // STOCK CARD
            modelBuilder.Entity<StockCard>()
                .Property(s => s.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<StockCard>()
                .HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockCard>()
                .HasOne(s => s.ProductBatch)
                .WithMany()
                .HasForeignKey(s => s.ProductBatchId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StockCard>()
                .HasOne(s => s.WarehouseBin)
                .WithMany()
                .HasForeignKey(s => s.WarehouseBinId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StockCard>()
                .HasIndex(s => new { s.ProductId, s.TransactionDate });

            // SUPPLIER - PRODUCT (NHÀ CUNG CẤP CHỈ CUNG CẤP SẢN PHẨM ĐƯỢC ĐĂNG KÝ)
            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Supplier)
                .WithMany(s => s.SupplierProducts)
                .HasForeignKey(sp => sp.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Product)
                .WithMany(p => p.SupplierProducts)
                .HasForeignKey(sp => sp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupplierProduct>()
                .HasIndex(sp => new { sp.SupplierId, sp.ProductId })
                .IsUnique();
        }
    }
}
