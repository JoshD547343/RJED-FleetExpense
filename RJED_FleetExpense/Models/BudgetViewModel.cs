namespace RJED_FleetExpense.Models
{
    // Posted by the Set/Update budget form
    public class BudgetInput
    {
        public int BudgetId { get; set; }          // 0 = new
        public int DepartmentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalAmount { get; set; }
        public int AlertThresholdPercent { get; set; } = 80;
        public bool BlockWhenExceeded { get; set; }
    }

    public class BudgetRow
    {
        public Budget Budget { get; set; } = null!;
        public decimal Approved { get; set; }   // reimbursed money
        public decimal Pending { get; set; }    // submitted, not reimbursed yet
        public decimal Total { get; set; }
        public string DepartmentName => Budget.Department?.DepartmentName ?? "Unknown";
        public decimal Committed => Approved;   // only reimbursed money is deducted
        public decimal Remaining => Budget.TotalAmount - Committed;
        public double UsedPct => Budget.TotalAmount == 0 ? 0 : (double)(Committed / Budget.TotalAmount * 100);

        public string State =>
            UsedPct >= 100 ? "Over" :
            UsedPct >= Budget.AlertThresholdPercent ? "Warning" : "OnTrack";
    }

    public class BudgetPageViewModel
    {
        public bool ShowAll { get; set; }
        public List<BudgetRow> Rows { get; set; } = new();
        public List<Department> Departments { get; set; } = new();

        public decimal TotalBudget => Rows.Sum(r => r.Budget.TotalAmount);
        public decimal TotalCommitted => Rows.Sum(r => r.Committed);
    }
}