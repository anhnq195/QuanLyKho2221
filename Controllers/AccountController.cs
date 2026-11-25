
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho2221.Data;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly WarehouseDbContext _context;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            WarehouseDbContext context)
        {
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // ĐĂNG NHẬP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
                username,
                password,
                false,
                false
            );

            if (result.Succeeded)
            {
                try
                {
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        UserName = username,
                        Action = "Đăng nhập",
                        Description = $"Tài khoản {username} đã đăng nhập vào hệ thống.",
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Không ảnh hưởng đến đăng nhập nếu ghi nhật ký thất bại.
                }

                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
            return View();
        }

        // ĐĂNG XUẤT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            string username = User.Identity?.Name ?? "Không xác định";

            try
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserName = username,
                    Action = "Đăng xuất",
                    Description = $"Tài khoản {username} đã đăng xuất khỏi hệ thống.",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Vẫn cho phép đăng xuất nếu ghi nhật ký thất bại.
            }

            await _signInManager.SignOutAsync();

            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
