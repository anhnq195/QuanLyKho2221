using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class ReceiptPrintController : Controller
    {
        private readonly WarehouseDbContext _context;

        public ReceiptPrintController(WarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Import(int id)
        {
            var receipt = await _context.ImportReceipts
                .Include(x => x.Product)
                .Include(x => x.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (receipt == null) return NotFound();
            if (receipt.Status != ReceiptStatus.Approved) return Forbid();
            return View(receipt);
        }

        public async Task<IActionResult> Export(int id)
        {
            var receipt = await _context.ExportReceipts
                .Include(x => x.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (receipt == null) return NotFound();
            if (receipt.Status != ReceiptStatus.Approved) return Forbid();
            return View(receipt);
        }
    }
}
