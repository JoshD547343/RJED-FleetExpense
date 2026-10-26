using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RJED_FleetExpense.ViewModels
{
    public class ExpenseViewModel
    {
        [Required]
        public string ExpenseType { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        public string PlateNumber { get; set; }

        [Required]
        public string Description { get; set; }

        public List<IFormFile> ReceiptFiles { get; set; }
    }
}