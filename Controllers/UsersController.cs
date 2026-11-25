using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho2221.Models;

namespace QuanLyKho2221.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UsersController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // DANH SÁCH VÀ TÌM KIẾM
        public async Task<IActionResult> Index(string? keyword)
        {
            var users = _userManager.Users.ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                users = users.Where(u =>
                    (u.UserName ?? "").Contains(keyword,
                        StringComparison.OrdinalIgnoreCase) ||
                    (u.Email ?? "").Contains(keyword,
                        StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var userList = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                userList.Add(new UserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    Role = string.Join(", ", roles),
                    IsLocked = user.LockoutEnd.HasValue &&
                               user.LockoutEnd.Value > DateTimeOffset.UtcNow
                });
            }

            ViewBag.Keyword = keyword;
            return View(userList);
        }

        // HIỂN THỊ FORM THÊM
        [HttpGet]
        public IActionResult Create()
        {
            return View("~/Views/Users/Create.cshtml");
        }

        // TẠO TÀI KHOẢN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Users/Create.cshtml", model);

            if (model.Role != "Admin" && model.Role != "Employee")
            {
                ModelState.AddModelError("Role", "Vai trò không hợp lệ.");
                return View("~/Views/Users/Create.cshtml", model);
            }

            if (await _userManager.FindByNameAsync(model.UserName) != null)
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập đã tồn tại.");
                return View("~/Views/Users/Create.cshtml", model);
            }

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email", "Email đã được sử dụng.");
                return View("~/Views/Users/Create.cshtml", model);
            }

            var user = new IdentityUser
            {
                UserName = model.UserName,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                var roleResult = await _userManager.AddToRoleAsync(user, model.Role);

                if (roleResult.Succeeded)
                {
                    TempData["Success"] = "Tạo tài khoản thành công!";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            else
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View("~/Views/Users/Create.cshtml", model);
        }

        // HIỂN THỊ FORM CHỈNH SỬA
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "Employee"
            };

            return View(model);
        }

        // LƯU CHỈNH SỬA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.Role != "Admin" && model.Role != "Employee")
            {
                ModelState.AddModelError("Role", "Vai trò không hợp lệ.");
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
                return NotFound();

            var sameName = await _userManager.FindByNameAsync(model.UserName);

            if (sameName != null && sameName.Id != user.Id)
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập đã tồn tại.");
                return View(model);
            }

            var sameEmail = await _userManager.FindByEmailAsync(model.Email);

            if (sameEmail != null && sameEmail.Id != user.Id)
            {
                ModelState.AddModelError("Email", "Email đã được sử dụng.");
                return View(model);
            }

            var oldRoles = await _userManager.GetRolesAsync(user);

            // Không cho tự hạ quyền Admin của chính mình
            if (user.UserName == User.Identity?.Name &&
                oldRoles.Contains("Admin") &&
                model.Role != "Admin")
            {
                ModelState.AddModelError("Role",
                    "Bạn không thể tự hạ quyền Admin của chính mình.");
                return View(model);
            }

            user.UserName = model.UserName;
            user.Email = model.Email;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            if (!oldRoles.Contains(model.Role))
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, oldRoles);

                if (!removeResult.Succeeded)
                {
                    TempData["Error"] = "Không thể cập nhật vai trò.";
                    return RedirectToAction(nameof(Index));
                }

                var addResult = await _userManager.AddToRoleAsync(user, model.Role);

                if (!addResult.Succeeded)
                {
                    TempData["Error"] = "Không thể cấp vai trò mới.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["Success"] = "Cập nhật tài khoản thành công!";
            return RedirectToAction(nameof(Index));
        }

        // HIỂN THỊ FORM ĐẶT LẠI MẬT KHẨU
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            return View(new ResetPasswordViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? ""
            });
        }

        // XỬ LÝ ĐẶT LẠI MẬT KHẨU
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
                return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(
                user, token, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Đặt lại mật khẩu thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // KHÓA / MỞ KHÓA TÀI KHOẢN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            if (user.UserName == User.Identity?.Name)
            {
                TempData["Error"] = "Không thể khóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                TempData["Error"] = "Không thể khóa tài khoản Admin bằng chức năng này.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.SetLockoutEnabledAsync(user, true);

            bool isLocked = user.LockoutEnd.HasValue &&
                            user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            var result = await _userManager.SetLockoutEndDateAsync(
                user,
                isLocked ? null : DateTimeOffset.MaxValue);

            TempData[result.Succeeded ? "Success" : "Error"] =
                result.Succeeded
                    ? (isLocked ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.")
                    : "Không thể thay đổi trạng thái tài khoản.";

            return RedirectToAction(nameof(Index));
        }

        // XÓA TÀI KHOẢN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            if (user.UserName == User.Identity?.Name)
            {
                TempData["Error"] = "Không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] = "Không thể xóa tài khoản Admin tại đây.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);

            TempData[result.Succeeded ? "Success" : "Error"] =
                result.Succeeded
                    ? "Xóa tài khoản thành công!"
                    : "Không thể xóa tài khoản.";

            return RedirectToAction(nameof(Index));
        }
    }
}