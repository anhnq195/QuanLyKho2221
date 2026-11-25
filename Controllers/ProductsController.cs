
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;
using System.Security.Cryptography;

namespace QuanLyKho2221.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly WarehouseDbContext _context;

        public ProductsController(WarehouseDbContext context)
        {
            _context = context;
        }

        // TẠO MÃ SẢN PHẨM NGẪU NHIÊN, KHÔNG TRÙNG
        private async Task<string> GenerateProductCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            for (int attempt = 0; attempt < 20; attempt++)
            {
                char[] randomChars = new char[6];

                for (int i = 0; i < randomChars.Length; i++)
                {
                    randomChars[i] = chars[
                        RandomNumberGenerator.GetInt32(chars.Length)
                    ];
                }

                string code = "SP-" + new string(randomChars);

                bool exists = await _context.Products
                    .AnyAsync(p => p.Code == code);

                if (!exists)
                    return code;
            }

            throw new InvalidOperationException(
                "Không thể tạo mã sản phẩm duy nhất. Vui lòng thử lại."
            );
        }

        // GHI NHẬT KÝ HOẠT ĐỘNG
        private void AddActivityLog(string action, string description)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserName = User.Identity?.Name ?? "Không xác định",
                Action = action,
                Description = description,
                CreatedAt = DateTime.Now
            });
        }

        // DANH SÁCH, TÌM KIẾM VÀ LỌC SẢN PHẨM
        public async Task<IActionResult> Index(
            string? searchString,
            int? categoryId,
            string? stockStatus,
            int? warehouseId,
            int? areaId,
            string? shelfCode,
            int? floorNumber,
            string? binCode)
        {
            await StockCardSeeder.EnsureProductCodesAsync(_context);

            // Thống kê tổng quan sản phẩm
            ViewBag.TotalProducts = await _context.Products
                .CountAsync();

            ViewBag.AvailableProducts = await _context.Products
                .CountAsync(p => p.Quantity > 5);

            ViewBag.LowStockProducts = await _context.Products
                .CountAsync(p => p.Quantity >= 1 && p.Quantity <= 5);

            ViewBag.OutOfStockProducts = await _context.Products
                .CountAsync(p => p.Quantity <= 0);

            // Danh sách sản phẩm cần cảnh báo
            ViewBag.LowStockList = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .Where(p => p.Quantity >= 1 && p.Quantity <= 5)
                .OrderBy(p => p.Quantity)
                .Take(10)
                .ToListAsync();

            // Danh sách sản phẩm đã hết hàng
            ViewBag.OutOfStockList = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .Where(p => p.Quantity <= 0)
                .OrderBy(p => p.Name)
                .Take(10)
                .ToListAsync();


            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Batches)
                    .ThenInclude(b => b.WarehouseBin)
                        .ThenInclude(bin => bin!.WarehouseShelf)
                            .ThenInclude(shelf => shelf!.WarehouseArea)
                                .ThenInclude(area => area!.Warehouse)
                .AsNoTracking();

            // Tìm theo tên hoặc mã sản phẩm
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string keyword = searchString.Trim();

                products = products.Where(p =>
                    p.Name.Contains(keyword) ||
                    (p.Code != null && p.Code.Contains(keyword)));
            }

            // Lọc danh mục
            if (categoryId.HasValue)
            {
                products = products.Where(p =>
                    p.CategoryId == categoryId.Value);
            }

            // Lọc tình trạng tồn kho
            if (stockStatus == "available")
                products = products.Where(p => p.Quantity > 5);
            else if (stockStatus == "low")
                products = products.Where(p =>
                    p.Quantity >= 1 && p.Quantity <= 5);
            else if (stockStatus == "out")
                products = products.Where(p => p.Quantity <= 0);

            // Lọc theo vị trí lưu trữ
            if (warehouseId.HasValue ||
                areaId.HasValue ||
                !string.IsNullOrWhiteSpace(shelfCode) ||
                floorNumber.HasValue ||
                !string.IsNullOrWhiteSpace(binCode))
            {
                products = products.Where(p =>
                    p.Batches.Any(b =>
                        b.WarehouseBin != null &&
                        b.WarehouseBin.WarehouseShelf != null &&
                        b.WarehouseBin.WarehouseShelf.WarehouseArea != null &&

                        (!warehouseId.HasValue ||
                            b.WarehouseBin.WarehouseShelf.WarehouseArea.WarehouseId == warehouseId.Value) &&

                        (!areaId.HasValue ||
                            b.WarehouseBin.WarehouseShelf.WarehouseArea.Id == areaId.Value) &&

                        (string.IsNullOrEmpty(shelfCode) ||
                            b.WarehouseBin.WarehouseShelf.Code == shelfCode) &&

                        (!floorNumber.HasValue ||
                            b.WarehouseBin.FloorNumber == floorNumber.Value) &&

                        (string.IsNullOrEmpty(binCode) ||
                            b.WarehouseBin.Code == binCode)
                    ));
            }


            // Danh sách danh mục
            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id", "Name", categoryId
            );

            // Danh sách kho
            ViewBag.Warehouses = new SelectList(
                await _context.Warehouses
                    .AsNoTracking()
                    .OrderBy(w => w.Name)
                    .ToListAsync(),
                "Id", "Name", warehouseId
            );

            // Danh sách khu vực
            var areasQuery = _context.WarehouseAreas
                .AsNoTracking()
                .AsQueryable();

            if (warehouseId.HasValue)
            {
                areasQuery = areasQuery.Where(a =>
                    a.WarehouseId == warehouseId.Value);
            }

            var areas = await areasQuery
                .OrderBy(a => a.Code)
                .ToListAsync();

            ViewBag.Areas = new SelectList(
                areas.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = a.Code + " - " + a.Name
                }),
                "Value", "Text", areaId
            );

            // Danh sách kệ - mã không trùng
            var shelvesQuery = _context.WarehouseShelves
                .Include(s => s.WarehouseArea)
                .AsNoTracking()
                .AsQueryable();

            if (areaId.HasValue)
            {
                shelvesQuery = shelvesQuery.Where(s =>
                    s.WarehouseAreaId == areaId.Value);
            }
            else if (warehouseId.HasValue)
            {
                shelvesQuery = shelvesQuery.Where(s =>
                    s.WarehouseArea!.WarehouseId == warehouseId.Value);
            }

            var shelfCodes = await shelvesQuery
                .Select(s => s.Code)
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync();

            ViewBag.Shelves = new SelectList(shelfCodes, shelfCode);

            // Danh sách tầng
            var floorsQuery = _context.WarehouseBins
                .AsNoTracking()
                .AsQueryable();

            if (areaId.HasValue)
            {
                floorsQuery = floorsQuery.Where(b =>
                    b.WarehouseShelf!.WarehouseAreaId == areaId.Value);
            }
            else if (warehouseId.HasValue)
            {
                floorsQuery = floorsQuery.Where(b =>
                    b.WarehouseShelf!.WarehouseArea!.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(shelfCode))
            {
                floorsQuery = floorsQuery.Where(b =>
                    b.WarehouseShelf!.Code == shelfCode);
            }

            var floors = await floorsQuery
                .Select(b => b.FloorNumber)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            ViewBag.Floors = new SelectList(
                floors.Select(f => new SelectListItem
                {
                    Value = f.ToString(),
                    Text = "Tầng " + f
                }),
                "Value", "Text", floorNumber
            );

            // Danh sách ô chứa - mã không trùng
            var binsQuery = _context.WarehouseBins
                .Include(b => b.WarehouseShelf)
                    .ThenInclude(s => s!.WarehouseArea)
                .AsNoTracking()
                .AsQueryable();

            if (areaId.HasValue)
            {
                binsQuery = binsQuery.Where(b =>
                    b.WarehouseShelf!.WarehouseAreaId == areaId.Value);
            }
            else if (warehouseId.HasValue)
            {
                binsQuery = binsQuery.Where(b =>
                    b.WarehouseShelf!.WarehouseArea!.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(shelfCode))
            {
                binsQuery = binsQuery.Where(b =>
                    b.WarehouseShelf!.Code == shelfCode);
            }

            if (floorNumber.HasValue)
            {
                binsQuery = binsQuery.Where(b =>
                    b.FloorNumber == floorNumber.Value);
            }

            var binCodes = await binsQuery
                .Select(b => b.Code)
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync();

            ViewBag.Bins = new SelectList(binCodes, binCode);

            // Giữ lại giá trị bộ lọc
            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.StockStatus = stockStatus;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.AreaId = areaId;
            ViewBag.ShelfCode = shelfCode;
            ViewBag.FloorNumber = floorNumber;
            ViewBag.BinCode = binCode;

            return View(await products
                .OrderByDescending(p => p.Id)
                .ToListAsync());

        }

        // TRUNG TÂM CẢNH BÁO TỒN KHO (T16, T62) & CẢNH BÁO HẠN SỬ DỤNG FEFO (T103)
        public async Task<IActionResult> StockAlerts()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .Where(p => p.Quantity <= 5)
                .OrderBy(p => p.Quantity)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var thresholdDate = DateTime.Today.AddDays(30);
            var expiryBatches = await _context.ProductBatches
                .Include(b => b.Product)
                .Include(b => b.WarehouseBin)
                    .ThenInclude(bin => bin!.WarehouseShelf)
                        .ThenInclude(s => s!.WarehouseArea)
                            .ThenInclude(a => a!.Warehouse)
                .AsNoTracking()
                .Where(b => b.Quantity > 0 &&
                            b.ExpiryDate.HasValue &&
                            b.ExpiryDate.Value.Date <= thresholdDate)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync();

            ViewBag.OutOfStock = products.Count(p => p.Quantity <= 0);
            ViewBag.LowStock = products.Count(p => p.Quantity >= 1 && p.Quantity <= 5);
            ViewBag.ExpiryBatches = expiryBatches;
            ViewBag.ExpiredCount = expiryBatches.Count(b => b.ExpiryDate!.Value.Date < DateTime.Today);
            ViewBag.NearExpiryCount = expiryBatches.Count(b => b.ExpiryDate!.Value.Date >= DateTime.Today);

            return View(products);
        }

        // LOAD DANH MỤC & NHÀ CUNG CẤP
        private async Task LoadCategories(IEnumerable<int>? selectedSupplierIds = null)
        {
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            ViewBag.CategoryId = new SelectList(
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id", "Name"
            );

            ViewBag.AllSuppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.SelectedSupplierIds = new HashSet<int>(selectedSupplierIds ?? Enumerable.Empty<int>());
        }

        // FORM THÊM
        public async Task<IActionResult> Create()
        {
            await LoadCategories();
            return View(new Product());
        }

        // XỬ LÝ THÊM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, int[]? supplierIds)
        {
            // Không cho người dùng tự nhập mã.
            product.Code = null;

            if (!ModelState.IsValid)
            {
                await LoadCategories(supplierIds);
                return View(product);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                await StockCardSeeder.EnsureSupplierProductsAsync(_context);

                // Tạo mã ngẫu nhiên và kiểm tra trùng.
                product.Code = await GenerateProductCode();
                product.CreatedAt = DateTime.Now;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                if (supplierIds != null && supplierIds.Length > 0)
                {
                    foreach (var sid in supplierIds.Distinct())
                    {
                        _context.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = sid,
                            ProductId = product.Id,
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                AddActivityLog(
                    "Thêm sản phẩm",
                    $"Đã thêm sản phẩm: {product.Name}, mã: {product.Code}, số lượng: {product.Quantity}."
                );

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Thêm sản phẩm thành công! Mã: {product.Code}";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Không thể thêm sản phẩm. Vui lòng thử lại."
                );

                await LoadCategories(supplierIds);
                return View(product);
            }
        }

        // CHI TIẾT SẢN PHẨM & LÔ HÀNG LƯU KHO (T13 - DETAIL PRODUCT SKU)
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            await StockCardSeeder.EnsureProductCodesAsync(_context);

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Batches)
                    .ThenInclude(b => b.WarehouseBin)
                        .ThenInclude(bin => bin!.WarehouseShelf)
                            .ThenInclude(shelf => shelf!.WarehouseArea)
                                .ThenInclude(area => area!.Warehouse)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (product == null)
                return NotFound();

            ViewBag.RecentImports = await _context.ImportReceipts
                .Include(r => r.Supplier)
                .AsNoTracking()
                .Where(r => r.ProductId == product.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentExports = await _context.ExportReceipts
                .Include(r => r.ProductBatch)
                .AsNoTracking()
                .Where(r => r.ProductId == product.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View(product);
        }

        // FORM SỬA
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            await StockCardSeeder.EnsureProductCodesAsync(_context);
            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            var product = await _context.Products
                .Include(p => p.SupplierProducts)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            var selectedSupplierIds = product.SupplierProducts.Select(sp => sp.SupplierId).ToList();
            await LoadCategories(selectedSupplierIds);
            return View(product);
        }

        // XỬ LÝ SỬA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product input, int[]? supplierIds)
        {
            if (id != input.Id)
                return NotFound();

            await StockCardSeeder.EnsureSupplierProductsAsync(_context);

            if (!string.IsNullOrWhiteSpace(input.Code))
            {
                input.Code = input.Code.Trim();

                bool codeExists = await _context.Products
                    .AnyAsync(p =>
                        p.Code == input.Code &&
                        p.Id != id
                    );

                if (codeExists)
                {
                    ModelState.AddModelError(
                        nameof(input.Code),
                        "Mã sản phẩm này đã được sử dụng bởi sản phẩm khác."
                    );
                }
            }

            if (ModelState.IsValid)
            {
                var product = await _context.Products
                    .Include(p => p.SupplierProducts)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                    return NotFound();

                string oldName = product.Name;
                string oldCode = product.Code ?? "";
                decimal oldPrice = product.Price;

                product.Name = input.Name;
                // Không bao giờ để trống mã sản phẩm: giữ mã cũ hoặc tự sinh nếu chưa có
                if (!string.IsNullOrWhiteSpace(input.Code))
                {
                    product.Code = input.Code;
                }
                else if (string.IsNullOrWhiteSpace(product.Code))
                {
                    product.Code = await GenerateProductCode();
                }

                product.Description = input.Description;
                product.Price = input.Price;
                product.CategoryId = input.CategoryId;
                product.HasExpiryDate = input.HasExpiryDate;

                // Cập nhật danh sách nhà cung cấp cung ứng sản phẩm này
                _context.SupplierProducts.RemoveRange(product.SupplierProducts);
                if (supplierIds != null && supplierIds.Length > 0)
                {
                    foreach (var sid in supplierIds.Distinct())
                    {
                        _context.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = sid,
                            ProductId = product.Id,
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                AddActivityLog(
                    "Sửa sản phẩm",
                    $"Đã cập nhật sản phẩm ID {id}. " +
                    $"Tên: {oldName} → {product.Name}; " +
                    $"Mã: {oldCode} → {product.Code}; " +
                    $"Giá: {oldPrice:N0} → {product.Price:N0}."
                );

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Cập nhật sản phẩm thành công.";

                return RedirectToAction(nameof(Index));
            }

            await LoadCategories(supplierIds);
            return View(input);
        }

        // FORM XÓA
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            bool hasImport = await _context.ImportReceipts
                .AnyAsync(r => r.ProductId == id.Value);

            bool hasExport = await _context.ExportReceipts
                .AnyAsync(r => r.ProductId == id.Value);

            bool hasInventoryCheck = await _context.InventoryCheckDetails
                .AnyAsync(d => d.ProductId == id.Value);

            ViewBag.CanDelete =
                !hasImport && !hasExport && !hasInventoryCheck;

            if (ViewBag.CanDelete == false)
            {
                ViewBag.DeleteWarning =
                    "Không thể xóa sản phẩm vì sản phẩm đã có lịch sử nhập kho, xuất kho hoặc kiểm kê.";
            }

            return View(product);
        }

        // XỬ LÝ XÓA
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            bool hasImport = await _context.ImportReceipts
                .AnyAsync(r => r.ProductId == id);

            bool hasExport = await _context.ExportReceipts
                .AnyAsync(r => r.ProductId == id);

            bool hasInventoryCheck = await _context.InventoryCheckDetails
                .AnyAsync(d => d.ProductId == id);

            if (hasImport || hasExport || hasInventoryCheck)
            {
                TempData["ErrorMessage"] =
                    "Không thể xóa sản phẩm vì đã có lịch sử nhập kho, xuất kho hoặc kiểm kê.";

                return RedirectToAction(nameof(Index));
            }

            string productName = product.Name;
            string productCode = product.Code ?? "";

            _context.Products.Remove(product);

            AddActivityLog(
                "Xóa sản phẩm",
                $"Đã xóa sản phẩm: {productName}, mã: {productCode}."
            );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Xóa sản phẩm thành công.";

            return RedirectToAction(nameof(Index));
        }
    }
}
