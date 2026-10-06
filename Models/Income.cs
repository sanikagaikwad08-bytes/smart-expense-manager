using System;
using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class Income
    {
        public int Id { get; set; }

        [Required]
        public string Source { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }
    }
}