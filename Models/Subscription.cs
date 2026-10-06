using System;
using System.ComponentModel.DataAnnotations;

namespace SmartExpenseManager.Models
{
    public class Subscription
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public decimal Cost { get; set; }

        [Required]
        public string BillingType { get; set; } = string.Empty;

        [Required]
        public DateTime NextPaymentDate { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;
    }
}