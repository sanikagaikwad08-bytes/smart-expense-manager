using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class ExpenseOptions
    {
        public static readonly string[] Categories =
            { "Food", "Transport", "Shopping", "Bills", "Health", "Education", "Entertainment", "Other" };

        public static readonly string[] PaymentMethods =
            { "Cash", "UPI", "Card", "Bank Transfer", "Other" };
    }
}