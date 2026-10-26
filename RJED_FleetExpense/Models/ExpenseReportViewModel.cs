using System;
using System.Collections.Generic;
using System.Linq;

namespace RJED_FleetExpense.Models
{
    public class ExpenseReportViewModel
    {
        // Filters (echoed back to the form)
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Category { get; set; }
        public string? Status { get; set; }
        public string? Department { get; set; }

        // True once the user has clicked "Generate Report"
        public bool Generated { get; set; }

        // Dropdown options
        public List<string> CategoryOptions { get; set; } = new();
        public List<string> DepartmentOptions { get; set; } = new();

        // Detail table
        public List<ExpenseReportRow> Rows { get; set; } = new();

        // Summary (all computed from Rows, so they always match the table)
        public int TotalClaims => Rows.Count;
        public decimal GrandTotal => Rows.Sum(r => r.Amount);
        public decimal TotalReimbursed => Rows.Where(r => r.Status == "Reimbursed").Sum(r => r.Amount);
        public decimal TotalPending => Rows.Where(r => r.Status != "Reimbursed" && r.Status != "Rejected").Sum(r => r.Amount);
        public int RejectedCount => Rows.Count(r => r.Status == "Rejected");
        public decimal AvgClaim => Rows.Count > 0 ? GrandTotal / Rows.Count : 0m;

        // Charts / breakdowns
        public List<string> TrendLabels { get; set; } = new();
        public List<decimal> TrendData { get; set; } = new();
        public List<BreakdownItem> ByCategory { get; set; } = new();
        public List<BreakdownItem> ByDepartment { get; set; } = new();
        public List<BreakdownItem> TopClaimants { get; set; } = new();
    }

    public class ExpenseReportRow
    {
        public int ExpenseId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Status { get; set; } = "";
    }

    public class BreakdownItem
    {
        public string Name { get; set; } = "";
        public string Sub { get; set; } = "";   // e.g. department under a claimant
        public int Count { get; set; }
        public decimal Total { get; set; }
        public double Pct { get; set; }
    }
}