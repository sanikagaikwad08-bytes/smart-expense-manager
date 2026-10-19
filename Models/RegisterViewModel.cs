using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Enter a username.")]
        [StringLength(30, MinimumLength = 3,
            ErrorMessage = "Username must be 3 to 30 characters.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your email.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a password.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm your password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password),
            ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
