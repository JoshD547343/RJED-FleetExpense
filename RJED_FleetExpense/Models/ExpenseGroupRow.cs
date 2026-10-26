namespace RJED_FleetExpense.Models
{
    public class ExpenseGroupRow
    {
        public string Label { get; set; } = "";
        public string? Sub { get; set; }
        public int Count { get; set; }
        public int ApprovedCount { get; set; }
        public decimal Total { get; set; }
        public decimal Approved { get; set; }
        public decimal Pending { get; set; }
        public decimal Rejected { get; set; }
        public double ApprovalRate => Count == 0 ? 0 : ApprovedCount * 100.0 / Count;
    }
}