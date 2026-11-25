
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.ApiControllers
{
    // =========================
    // DTO: DỮ LIỆU API
    // =========================

    public class ProductCreateRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal Price { get; set; }

        public int? CategoryId { get; set; }
    }

    public class ProductUpdateRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal Price { get; set; }

        public int? CategoryId { get; set; }
    }

    public class ImportCreateRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Nhà cung cấp không hợp lệ")]
        public int SupplierId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Sản phẩm không hợp lệ")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Ô chứa không hợp lệ")]
        public int WarehouseBinId { get; set; }

        [Required, StringLength(50)]
        public string BatchCode { get; set; } = string.Empty;

        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0")]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999")]
        public decimal UnitPrice { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class ExportCreateRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Lô hàng không hợp lệ")]
        public int ProductBatchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng xuất phải lớn hơn 0")]
        public int Quantity { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999")]
        public decimal UnitPrice { get; set; }

        [StringLength(150)]
        public string? Receiver { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    // =========================
    // API HÀNG HÓA
    // =========================

    [ApiController]
    [Route("api/products")]
    [Authorize]
    public class ProductsApiController : ControllerBase
    {
        private readonly WarehouseDbContext _context;

        public ProductsApiController(WarehouseDbContext context)
        {
            _context = context;
        }

        private async Task<string> GenerateProductCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            for (int attempt = 0; attempt < 20; attempt++)
            {
                char[] randomChars = new char[6];

                for (int i = 0; i < randomChars.Length; i++)
                {
                    randomChars[i] =
                        chars[RandomNumberGenerator.GetInt32(chars.Length)];
                }

                string code = "SP-" + new string(randomChars);

                if (!await _context.Products.AnyAsync(p => p.Code == code))
                    return code;
            }

            throw new InvalidOperationException(
                "Không thể tạo mã sản phẩm duy nhất.");
        }

        private void AddLog(string action, string description)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = action,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        // GET: api/products
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.Description,
                    p.Quantity,
                    p.Price,
                    p.CategoryId,
                    CategoryName = p.Category != null
                        ? p.Category.Name
                        : null,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                total = products.Count,
                data = products
            });
        }

        // GET: api/products/1
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.Description,
                    p.Quantity,
                    p.Price,
                    p.CategoryId,
                    CategoryName = p.Category != null
                        ? p.Category.Name
                        : null,
                    p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sản phẩm."
                });
            }

            return Ok(new
            {
                success = true,
                data = product
            });
        }

        // POST: api/products
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] ProductCreateRequest request)
        {
            if (request.CategoryId.HasValue)
            {
                bool categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == request.CategoryId.Value);

                if (!categoryExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Danh mục không tồn tại."
                    });
                }
            }

            try
            {
                string code = await GenerateProductCode();

                var product = new Product
                {
                    Code = code,
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Quantity = request.Quantity,
                    Price = request.Price,
                    CategoryId = request.CategoryId,
                    CreatedAt = DateTime.Now
                };

                _context.Products.Add(product);

                AddLog(
                    "Thêm sản phẩm",
                    $"API thêm sản phẩm {product.Name}, mã {code}.");

                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = product.Id },
                    new
                    {
                        success = true,
                        message = "Thêm sản phẩm thành công.",
                        data = new
                        {
                            product.Id,
                            product.Code,
                            product.Name,
                            product.Description,
                            product.Quantity,
                            product.Price,
                            product.CategoryId,
                            product.CreatedAt
                        }
                    });
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Không thể lưu sản phẩm. Có thể mã sản phẩm đã bị trùng."
                });
            }
        }

        // PUT: api/products/1
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] ProductUpdateRequest request)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sản phẩm."
                });
            }

            if (request.CategoryId.HasValue)
            {
                bool categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == request.CategoryId.Value);

                if (!categoryExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Danh mục không tồn tại."
                    });
                }
            }

            string oldName = product.Name;

            product.Name = request.Name.Trim();
            product.Description = request.Description?.Trim();
            product.Price = request.Price;
            product.CategoryId = request.CategoryId;

            AddLog(
                "Sửa sản phẩm",
                $"API cập nhật sản phẩm ID {id}: {oldName} → {product.Name}.");

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Cập nhật sản phẩm thành công.",
                data = new
                {
                    product.Id,
                    product.Code,
                    product.Name,
                    product.Description,
                    product.Quantity,
                    product.Price,
                    product.CategoryId,
                    product.CreatedAt
                }
            });
        }

        // DELETE: api/products/1
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sản phẩm."
                });
            }

            bool hasImport = await _context.ImportReceipts
                .AnyAsync(x => x.ProductId == id);

            bool hasExport = await _context.ExportReceipts
                .AnyAsync(x => x.ProductId == id);

            bool hasInventoryCheck = await _context.InventoryCheckDetails
                .AnyAsync(x => x.ProductId == id);

            if (hasImport || hasExport || hasInventoryCheck)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Không thể xóa sản phẩm vì đã có lịch sử nhập, xuất hoặc kiểm kê."
                });
            }

            string name = product.Name;

            _context.Products.Remove(product);

            AddLog(
                "Xóa sản phẩm",
                $"API xóa sản phẩm {name}, ID {id}.");

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Xóa sản phẩm thành công."
            });
        }
    }

    // =========================
    // API NHẬP KHO
    // =========================

    [ApiController]
    [Route("api/imports")]
    [Authorize]
    public class ImportsApiController : ControllerBase
    {
        private readonly WarehouseDbContext _context;

        public ImportsApiController(WarehouseDbContext context)
        {
            _context = context;
        }

        // GET: api/imports
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var receipts = await _context.ImportReceipts
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.Supplier)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.ReceiptCode,
                    x.SupplierId,
                    SupplierName = x.Supplier != null
                        ? x.Supplier.Name : null,
                    x.ProductId,
                    ProductName = x.Product != null
                        ? x.Product.Name : null,
                    x.Quantity,
                    x.Status,
                    x.UnitPrice,
                    TotalAmount = x.Quantity * x.UnitPrice,
                    x.Note,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                total = receipts.Count,
                data = receipts
            });
        }

        // GET: api/imports/1
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var receipt = await _context.ImportReceipts
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.Supplier)
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.ReceiptCode,
                    x.SupplierId,
                    SupplierName = x.Supplier != null
                        ? x.Supplier.Name : null,
                    x.ProductId,
                    ProductName = x.Product != null
                        ? x.Product.Name : null,
                    x.Quantity,
                    x.Status,
                    x.UnitPrice,
                    TotalAmount = x.Quantity * x.UnitPrice,
                    x.Note,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (receipt == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy phiếu nhập."
                });
            }

            return Ok(new
            {
                success = true,
                data = receipt
            });
        }

        // POST: api/imports - tạo phiếu chờ duyệt, không cộng tồn ngay.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ImportCreateRequest request)
        {
            var supplierExists = await _context.Suppliers.AnyAsync(x => x.Id == request.SupplierId);
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId);
            var binExists = await _context.WarehouseBins.AnyAsync(x => x.Id == request.WarehouseBinId);

            if (!supplierExists || product == null || !binExists)
                return BadRequest(new { success = false, message = "Nhà cung cấp, sản phẩm hoặc ô chứa không hợp lệ." });

            if (string.IsNullOrWhiteSpace(request.BatchCode))
                return BadRequest(new { success = false, message = "Vui lòng nhập mã lô hàng." });

            if (product.HasExpiryDate && !request.ExpiryDate.HasValue)
                return BadRequest(new { success = false, message = "Sản phẩm bắt buộc có hạn sử dụng." });

            if (request.ManufactureDate.HasValue && request.ExpiryDate.HasValue &&
                request.ManufactureDate.Value.Date > request.ExpiryDate.Value.Date)
                return BadRequest(new { success = false, message = "Hạn sử dụng phải sau ngày sản xuất." });

            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value.Date < DateTime.Today)
                return BadRequest(new { success = false, message = "Lô hàng đã hết hạn." });

            var receipt = new ImportReceipt
            {
                ReceiptCode = "TEMP-" + Guid.NewGuid().ToString("N"),
                SupplierId = request.SupplierId,
                ProductId = request.ProductId,
                WarehouseBinId = request.WarehouseBinId,
                BatchCode = request.BatchCode.Trim(),
                ManufactureDate = request.ManufactureDate,
                ExpiryDate = request.ExpiryDate,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                Note = request.Note?.Trim(),
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity?.Name,
                Status = ReceiptStatus.Pending
            };

            _context.ImportReceipts.Add(receipt);
            await _context.SaveChangesAsync();
            receipt.ReceiptCode = "PN" + receipt.Id.ToString("D3");
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = "Tạo phiếu nhập chờ duyệt",
                Description = $"API tạo phiếu nhập {receipt.ReceiptCode}, sản phẩm: {product.Name}, số lượng: {receipt.Quantity}",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, new
            {
                success = true,
                message = "Đã tạo phiếu nhập chờ duyệt. Tồn kho chưa thay đổi.",
                data = new { receipt.Id, receipt.ReceiptCode, receipt.Status, receipt.SupplierId,
                    receipt.ProductId, receipt.WarehouseBinId, receipt.BatchCode, receipt.Quantity,
                    receipt.UnitPrice, receipt.Note, receipt.CreatedAt }
            });
        }

    }

    // =========================
    // API XUẤT KHO
    // =========================

    [ApiController]
    [Route("api/exports")]
    [Authorize]
    public class ExportsApiController : ControllerBase
    {
        private readonly WarehouseDbContext _context;

        public ExportsApiController(WarehouseDbContext context)
        {
            _context = context;
        }

        // GET: api/exports
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var receipts = await _context.ExportReceipts
                .AsNoTracking()
                .Include(x => x.Product)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.ReceiptCode,
                    x.ProductId,
                    ProductName = x.Product != null
                        ? x.Product.Name : null,
                    x.Quantity,
                    x.Status,
                    x.UnitPrice,
                    TotalAmount = x.Quantity * x.UnitPrice,
                    x.Receiver,
                    x.Note,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                total = receipts.Count,
                data = receipts
            });
        }

        // GET: api/exports/1
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var receipt = await _context.ExportReceipts
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.ReceiptCode,
                    x.ProductId,
                    ProductName = x.Product != null
                        ? x.Product.Name : null,
                    x.Quantity,
                    x.Status,
                    x.UnitPrice,
                    TotalAmount = x.Quantity * x.UnitPrice,
                    x.Receiver,
                    x.Note,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (receipt == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy phiếu xuất."
                });
            }

            return Ok(new
            {
                success = true,
                data = receipt
            });
        }

        // POST: api/exports - tạo phiếu chờ duyệt, không trừ tồn ngay.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExportCreateRequest request)
        {
            var batch = await _context.ProductBatches
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == request.ProductBatchId);

            if (batch == null || batch.Product == null)
                return NotFound(new { success = false, message = "Không tìm thấy lô hàng." });

            if (batch.Quantity < request.Quantity)
                return BadRequest(new { success = false, message = $"Lô hàng hiện chỉ còn {batch.Quantity} sản phẩm." });

            var receipt = new ExportReceipt
            {
                ReceiptCode = "TEMP-" + Guid.NewGuid().ToString("N"),
                ProductId = batch.ProductId,
                ProductBatchId = batch.Id,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                Receiver = request.Receiver?.Trim(),
                Note = request.Note?.Trim(),
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity?.Name,
                Status = ReceiptStatus.Pending
            };

            _context.ExportReceipts.Add(receipt);
            await _context.SaveChangesAsync();
            receipt.ReceiptCode = "PX" + receipt.Id.ToString("D3");
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = "Tạo phiếu xuất chờ duyệt",
                Description = $"API tạo phiếu xuất {receipt.ReceiptCode}, sản phẩm: {batch.Product.Name}, số lượng: {receipt.Quantity}",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, new
            {
                success = true,
                message = "Đã tạo phiếu xuất chờ duyệt. Tồn kho chưa thay đổi.",
                data = new { receipt.Id, receipt.ReceiptCode, receipt.Status, receipt.ProductId,
                    receipt.ProductBatchId, receipt.Quantity, receipt.UnitPrice, receipt.Receiver,
                    receipt.Note, receipt.CreatedAt }
            });
        }

    }
}
