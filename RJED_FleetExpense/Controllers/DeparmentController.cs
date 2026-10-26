using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RJED_FleetExpense.Controllers
{
    
    [Authorize(Roles = "Department_head")]
    [Route("Department")]
    public class DepartmentController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DepartmentController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        private static readonly string[] ApprovedStatuses =
            { "DepartmentApproved", "ForAccounting", "Reimbursed" };

        // ===================== DASHBOARD =====================
        [HttpGet]
        [Route("")]
        [Route("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            ViewBag.TotalExpensesThisMonth = await _context.Expenses
                .Where(e => e.ExpenseDate.Month == DateTime.Now.Month &&
                            e.ExpenseDate.Year == DateTime.Now.Year)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.TotalApproved = await _context.Expenses
                .Where(e => ApprovedStatuses.Contains(e.Status))
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.TotalRejected = await _context.Expenses
                .Where(e => e.Status == "Rejected")
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.CurrentUserName = user.FullName ?? user.UserName ?? "Department Head";
            ViewBag.CurrentUserEmail = user.Email ?? "—";

            // Recent decisions only (approved at department level, or rejected).
            // Expenses still waiting on the supervisor or on this approver are not shown here.
            var recentExpenses = await _context.Expenses
                .Include(e => e.User)
                .Where(e => ApprovedStatuses.Contains(e.Status) || e.Status == "Rejected")
                .OrderByDescending(e => e.DateSubmitted)
                .Take(8)
                .ToListAsync();

            ViewData["Title"] = "Department Dashboard";
            return View(recentExpenses);
        }

        // ===================== EXPENSES (reimbursed only) =====================
        [HttpGet]
        [Route("Expenses")]
        public async Task<IActionResult> Expenses(string search, string category, string dateFrom, string dateTo)
        {
            // Only reimbursed items count as expenses. Pending, approved and rejected do not.
            var reimbursed = _context.Expenses
                .Include(e => e.User)
                .Include(e => e.Reimbursement)
                .Where(e => e.Status == "Reimbursed");

            var query = reimbursed;

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e =>
                    (e.Description != null && e.Description.Contains(search)) ||
                    e.ExpenseType.Contains(search) ||
                    (e.PlateNumber != null && e.PlateNumber.Contains(search)) ||
                    (e.User.FullName != null && e.User.FullName.Contains(search)) ||
                    (e.User.UserName != null && e.User.UserName.Contains(search)));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(e => e.ExpenseType == category);

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from);

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                var toExclusive = to.Date.AddDays(1);
                query = query.Where(e => e.ExpenseDate < toExclusive);
            }

            var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            // Summary cards (follow the current filters)
            ViewBag.FilteredTotal = expenses.Sum(e => e.Amount);
            ViewBag.FilteredCount = expenses.Count;

            var now = DateTime.Now;
            ViewBag.ThisMonthTotal = expenses
                .Where(e => e.ExpenseDate.Month == now.Month && e.ExpenseDate.Year == now.Year)
                .Sum(e => e.Amount);

            ViewBag.Categories = await reimbursed
                .Select(e => e.ExpenseType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            ViewData["Title"] = "Expenses";
            return View(expenses);
        }

        // ===================== FOR APPROVAL =====================
        [HttpGet]
        [Route("ForApproval")]
        public async Task<IActionResult> ForApproval(string search, string dateFrom, string dateTo)
        {
            var query = _context.Expenses
                .Include(e => e.User)
                .Where(e => e.Status == "SupervisorApproved")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e =>
                    (e.Description != null && e.Description.Contains(search)) ||
                    e.ExpenseType.Contains(search) ||
                    (e.PlateNumber != null && e.PlateNumber.Contains(search)) ||
                    (e.User.FullName != null && e.User.FullName.Contains(search)) ||
                    (e.User.UserName != null && e.User.UserName.Contains(search)));

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from);

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
                query = query.Where(e => e.ExpenseDate <= to);

            var expenses = await query.OrderByDescending(e => e.DateSubmitted).ToListAsync();

            ViewBag.Search = search;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.TotalPendingAmount = expenses.Sum(e => e.Amount);
            ViewBag.TotalPendingCount = expenses.Count;

            ViewData["Title"] = "Final Approval";
            return View(expenses);
        }

        // ===================== DETAILS =====================
        [HttpGet]
        [Route("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(expense.PlateNumber))
            {
                ViewBag.VehicleType = await _context.Vehicles
                    .Where(v => v.PlateNumber == expense.PlateNumber)
                    .Select(v => v.Type)
                    .FirstOrDefaultAsync();
            }

            ViewData["Title"] = "Expense Details";
            return View(expense);
        }

        // ===================== APPROVE =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Approve/{id}")]
        public async Task<IActionResult> Approve(int id, string? remarks = null)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            if (expense.Status != "SupervisorApproved")
            {
                TempData["Error"] = "Only expenses approved by Supervisor can be processed.";
                return RedirectToAction(nameof(ForApproval));
            }

            expense.Status = "DepartmentApproved";
            expense.DepartmentStatus = "Approved";
            expense.DepartmentApprovedDate = DateTime.Now;
            expense.DepartmentRemarks = remarks?.Trim();
            expense.ForwardedTo = "Accounting";
            expense.IsForwarded = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Expense approved and forwarded to Accounting.";
            return RedirectToAction(nameof(ForApproval));
        }

        // ===================== REJECT =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Reject/{id}")]
        public async Task<IActionResult> Reject(int id, string? remarks = null, string? rejectionReason = null)
        {
            if (string.IsNullOrWhiteSpace(remarks))
            {
                TempData["Error"] = "Remarks are required when rejecting an expense.";
                return RedirectToAction(nameof(ForApproval));
            }

            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            if (expense.Status != "SupervisorApproved")
            {
                TempData["Error"] = "This expense cannot be rejected at this stage.";
                return RedirectToAction(nameof(ForApproval));
            }

            expense.Status = "Rejected";
            expense.DepartmentStatus = "Rejected";
            expense.DepartmentRejectedDate = DateTime.Now;
            expense.DepartmentRemarks = remarks.Trim();
            expense.RejectionReason = rejectionReason?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Expense rejected.";
            return RedirectToAction(nameof(ForApproval));
        }

        // ===================== MY TEAM =====================
        [HttpGet]
        [Route("MyTeam")]
        public async Task<IActionResult> MyTeam()
        {
            var users = await _userManager.Users.ToListAsync();
            ViewData["Title"] = "My Team";
            return View(users);
        }

        // ===================== BUDGET MONITORING =====================
        [HttpGet]
        [Route("BudgetMonitoring")]
        public async Task<IActionResult> BudgetMonitoring()
        {
            var currentYear = DateTime.Now.Year;
            var monthlyData = await _context.Expenses
                .Where(e => e.ExpenseDate.Year == currentYear &&
                            ApprovedStatuses.Contains(e.Status))
                .GroupBy(e => e.ExpenseDate.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderBy(g => g.Month)
                .ToListAsync();

            var monthlyTotals = new decimal[12];
            foreach (var m in monthlyData)
                monthlyTotals[m.Month - 1] = m.Total;

            ViewBag.MonthlyTotals = monthlyTotals;

            var byType = await _context.Expenses
                .Where(e => ApprovedStatuses.Contains(e.Status))
                .GroupBy(e => e.ExpenseType)
                .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                .OrderByDescending(g => g.Total)
                .ToListAsync();

            ViewBag.ByType = byType;

            ViewBag.TotalApproved = await _context.Expenses
                .Where(e => ApprovedStatuses.Contains(e.Status))
                .SumAsync(e => e.Amount);

            ViewBag.TotalPending = await _context.Expenses
                .Where(e => e.Status == "SupervisorApproved")
                .SumAsync(e => e.Amount);

            ViewBag.TotalRejected = await _context.Expenses
                .Where(e => e.Status == "Rejected")
                .SumAsync(e => e.Amount);

            ViewData["Title"] = "Budget Monitoring";
            return View();
        }

        // ===================== FINANCIAL LOG =====================
        [HttpGet]
        [Route("FinancialLog")]
        public async Task<IActionResult> FinancialLog(string search, string status,
            string expenseType, string dateFrom, string dateTo,
            string sortBy = "date_desc", int page = 1)
        {
            const int pageSize = 15;
            if (page < 1) page = 1;

            var query = _context.Expenses.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.Description.Contains(search) ||
                                         e.ExpenseType.Contains(search));

            // The dropdown shows simplified statuses; map them to the real workflow statuses
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Approved")
                    query = query.Where(e => ApprovedStatuses.Contains(e.Status));
                else if (status == "Pending")
                    query = query.Where(e => e.Status == "Pending" || e.Status == "SupervisorApproved");
                else
                    query = query.Where(e => e.Status == status);
            }

            if (!string.IsNullOrEmpty(expenseType))
                query = query.Where(e => e.ExpenseType == expenseType);

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from);

            // Include the whole "to" day
            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                var toExclusive = to.Date.AddDays(1);
                query = query.Where(e => e.ExpenseDate < toExclusive);
            }

            // ---- Summary cards (based on the filtered query, before paging/sorting) ----
            ViewBag.FilteredTotal = await query
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.FilteredApproved = await query
                .Where(e => ApprovedStatuses.Contains(e.Status))
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.FilteredPending = await query
                .Where(e => e.Status == "Pending" || e.Status == "SupervisorApproved")
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            ViewBag.FilteredRejected = await query
                .Where(e => e.Status == "Rejected")
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            query = sortBy switch
            {
                "date_asc" => query.OrderBy(e => e.ExpenseDate),
                "amount_desc" => query.OrderByDescending(e => e.Amount),
                "amount_asc" => query.OrderBy(e => e.Amount),
                "status" => query.OrderBy(e => e.Status),
                _ => query.OrderByDescending(e => e.DateSubmitted)
            };

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var expenses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.ExpenseTypes = await _context.Expenses
                .Select(e => e.ExpenseType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.ExpenseType = expenseType;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SortBy = sortBy;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            ViewData["Title"] = "Financial Log";
            return View(expenses);
        }
    }
}