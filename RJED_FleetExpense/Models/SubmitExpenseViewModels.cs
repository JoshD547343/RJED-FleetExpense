using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RJED_FleetExpense.ViewModels
{
    public class SubmitExpenseViewModel
    {
        [Required]
        public string ExpenseType { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        public string? PlateNumber { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        public List<IFormFile>? ReceiptFiles { get; set; }
    }
}