using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.Services
{
    public class BudgetCheckResult
    {
        public bool Allowed { get; set; } = true;
        public string? Message { get; set; }
        public bool IsWarningOnly => Allowed && Message != null;
    }

    public class BudgetService
    {
        private readonly ApplicationDbContext _context;
        public BudgetService(ApplicationDbContext context) => _context = context;

        // Statuses that mean the employee has been reimbursed.
        // These are the only expenses deducted from the budget.
        // If you add a "Reimbursed" / "Paid" status, change it here.
        public static readonly string[] ReimbursedStatuses = { "Reimbursed" };

        /// currentOnly = true  -> budgets whose period includes onDate (default today)
        /// currentOnly = false -> all active budgets
        public async Task<List<BudgetRow>> GetRowsAsync(DateTime? onDate = null, bool currentOnly = true)
        {
            var q = _context.Budgets.Include(b => b.Department).Where(b => b.IsActive);

            if (currentOnly)
            {
                var d = (onDate ?? DateTime.Today).Date;
                q = q.Where(b => b.StartDate <= d && b.EndDate >= d);
            }

            var budgets = await q
                .OrderByDescending(b => b.StartDate)
                .ThenBy(b => b.Department.DepartmentName)
                .ToListAsync();

            if (budgets.Count == 0) return new List<BudgetRow>();

            var minStart = budgets.Min(b => b.StartDate).Date;
            var maxEnd = budgets.Max(b => b.EndDate).Date.AddDays(1);

            var expenses = await _context.Expenses
                .Include(e => e.User)
                .Where(e => e.ExpenseDate >= minStart &&
                            e.ExpenseDate < maxEnd &&
                            e.Status != "Rejected")
                .ToListAsync();

            return budgets.Select(b =>
            {
                var name = b.Department?.DepartmentName;
                var from = b.StartDate.Date;
                var to = b.EndDate.Date.AddDays(1);

                var scope = expenses.Where(e =>
                    e.User != null &&
                    e.User.Department == name &&
                    e.ExpenseDate >= from &&
                    e.ExpenseDate < to).ToList();

                return new BudgetRow
                {
                    Budget = b,
                    // "Approved" on the row = reimbursed money (deducted from budget)
                    Approved = scope.Where(e => ReimbursedStatuses.Contains(e.Status)).Sum(e => e.Amount),
                    // "Pending" on the row = submitted but not reimbursed yet (not deducted)
                    Pending = scope.Where(e => !ReimbursedStatuses.Contains(e.Status)).Sum(e => e.Amount)
                };
            }).ToList();
        }

        /// Reimbursed spend for a department between two dates (inclusive)
        public async Task<decimal> GetCommittedAsync(string? departmentName, DateTime start, DateTime end)
        {
            if (string.IsNullOrWhiteSpace(departmentName)) return 0;
            var from = start.Date;
            var to = end.Date.AddDays(1);

            return await _context.Expenses
                .Where(e => e.User.Department == departmentName &&
                            e.ExpenseDate >= from &&
                            e.ExpenseDate < to &&
                            ReimbursedStatuses.Contains(e.Status))
                .SumAsync(e => e.Amount);
        }

        /// Call BEFORE saving a new expense, and again at approval time.
        /// Only reimbursed money counts toward the budget, so the pending expense
        /// itself is never already included; excludeAmount is kept for compatibility
        /// with existing callers and is no longer needed.
        public async Task<BudgetCheckResult> CheckAsync(
            string? department, DateTime expenseDate, decimal newAmount, decimal excludeAmount = 0)
        {
            var result = new BudgetCheckResult();
            if (string.IsNullOrWhiteSpace(department)) return result;

            var rows = await GetRowsAsync(expenseDate, currentOnly: true);

            foreach (var r in rows.Where(r => r.DepartmentName == department))
            {
                var after = r.Committed + newAmount;

                if (after > r.Budget.TotalAmount)
                {
                    result.Message =
                        $"This would put {department} over budget ({after:N2} of {r.Budget.TotalAmount:N2}).";
                    if (r.Budget.BlockWhenExceeded)
                    {
                        result.Allowed = false;
                        return result;
                    }
                }
                else if (result.Message == null && r.Budget.TotalAmount > 0)
                {
                    var pct = (double)(after / r.Budget.TotalAmount * 100);
                    if (pct >= r.Budget.AlertThresholdPercent)
                        result.Message = $"{department} will be at {pct:0.#}% of its budget.";
                }
            }
            return result;
        }
    }
}