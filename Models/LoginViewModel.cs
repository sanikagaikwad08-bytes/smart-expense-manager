using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Enter your username or email.")]
        public string LoginId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
