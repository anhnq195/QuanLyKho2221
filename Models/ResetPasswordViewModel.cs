using System.ComponentModel.DataAnnotations;

namespace QuanLyKho2221.Models
{
    public class ResetPasswordViewModel
    {
        public string Id { get; set; } = "";

        public string UserName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = "";
    }
}