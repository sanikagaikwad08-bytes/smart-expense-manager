using System;
using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public string? Description { get; set; }
    }
}