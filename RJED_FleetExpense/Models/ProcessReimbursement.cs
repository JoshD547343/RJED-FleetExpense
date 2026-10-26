using System;
using System.Collections.Generic;

namespace RJED_FleetExpense.Models
{
    public class ProcessReimbursementViewModel
    {
        // Filters (echoed back to the form)
        public string? Search { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // "pending" or "history"
        public string Tab { get; set; } = "pending";

        // Summary cards (not affected by the filters)
        public int PayableCount { get; set; }
        public decimal PayableAmount { get; set; }
        public decimal PaidThisMonth { get; set; }
        public int PaidThisMonthCount { get; set; }

        public List<PayableExpenseRow> Payable { get; set; } = new();
        public List<PaymentHistoryRow> History { get; set; } = new();
    }

    // An approved expense waiting to be paid
    public class PayableExpenseRow
    {
        public int ExpenseId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime? ExpenseDate { get; set; }
        public DateTime? DateSubmitted { get; set; }
    }

    // A payment that has already been made
    public class PaymentHistoryRow
    {
        public int ReimbursementId { get; set; }
        public int ExpenseId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Category { get; set; } = "";
        public string? TransactionRef { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; }
        public string ProcessedByName { get; set; } = "";
    }
}