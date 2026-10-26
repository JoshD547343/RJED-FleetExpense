using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using RJED_FleetExpense.Services;
using RJED_FleetExpense.ViewModels;

namespace RJED_FleetExpense.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditService;
        private readonly BudgetService _budgetService;

        // Statuses that mean "approved, waiting for Accounting to pay"
        private static readonly string[] ApprovedStatuses = { "Approved", "DepartmentApproved", "ForAccounting" };

        public EmployeeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditService,
            BudgetService budgetService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _budgetService = budgetService;
        }

        // ═══════════════════════════════════════
        // DASHBOARD
        // ═══════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var employeeId = user.Id;
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var model = new EmployeeDashboardViewModel
            {
                CurrentUserName = user.FullName ?? user.UserName ?? "Employee",
                CurrentUserEmail = user.Email ?? "",
                EmployeeId = $"EMP-{user.Id.Substring(0, 6).ToUpper()}"
            };

            model.MyExpenses = await _context.Expenses
                .Where(e => e.UserId == employeeId)
                .OrderByDescending(e => e.ExpenseDate)
                .Take(10)
                .ToListAsync();

            model.MyPendingExpenses = model.MyExpenses.Count(e => e.Status == "Pending");
            model.MyTotalThisMonth = model.MyExpenses.Count(e =>
                e.ExpenseDate.Month == currentMonth && e.ExpenseDate.Year == currentYear);
            model.MyTotalThisMonthAmount = model.MyExpenses
                .Where(e => e.ExpenseDate.Month == currentMonth && e.ExpenseDate.Year == currentYear)
                .Sum(e => e.Amount);
            model.RecentlyApproved = model.MyExpenses.Count(e => e.Status == "Approved");
            model.RejectedThisMonth = model.MyExpenses.Count(e =>
                e.Status == "Rejected" &&
                e.ExpenseDate.Month == currentMonth &&
                e.ExpenseDate.Year == currentYear);

            return View(model);
        }

        // ═══════════════════════════════════════
        // SUBMIT EXPENSE PAGE (GET)
        // ═══════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Submit_Expense()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var employeeId = user.Id;

            var model = new EmployeeDashboardViewModel
            {
                CurrentUserName = user.FullName ?? user.UserName ?? "Employee",
                CurrentUserEmail = user.Email ?? "",
                EmployeeId = $"EMP-{user.Id.Substring(0, 6).ToUpper()}"
            };

            model.MyExpenses = await _context.Expenses
                .Where(e => e.UserId == employeeId)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            // Only Active vehicles can be picked (not Maintenance / Retired)
            model.Vehicles = await _context.Vehicles
                .Where(v => v.Status == "Active")
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            // Dates each vehicle is already used (any employee; rejected expenses don't count)
            var booked = await _context.Expenses
                .Where(e => e.PlateNumber != null && e.Status != "Rejected")
                .Select(e => new { e.PlateNumber, e.ExpenseDate })
                .ToListAsync();

            model.BookedDates = booked
                .GroupBy(x => x.PlateNumber!)
                .ToDictionary(g => g.Key,
                              g => g.Select(x => x.ExpenseDate.ToString("yyyy-MM-dd")).Distinct().ToList());

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit_Expense(SubmitExpenseViewModel model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please complete all required fields.";
                    return RedirectToAction(nameof(Submit_Expense));
                }

                // ───────── VEHICLE CHECK ─────────
                string? plate = null;
                if (!string.IsNullOrWhiteSpace(model.PlateNumber))
                {
                    plate = model.PlateNumber.Trim().ToUpperInvariant();

                    var vehicleOk = await _context.Vehicles
                        .AnyAsync(v => v.PlateNumber == plate && v.Status == "Active");
                    if (!vehicleOk)
                    {
                        TempData["ErrorMessage"] = "Please select a valid, active vehicle.";
                        return RedirectToAction(nameof(Submit_Expense));
                    }

                    var day = model.ExpenseDate.Date;
                    var next = day.AddDays(1);

                    var alreadyUsed = await _context.Expenses.AnyAsync(e =>
                        e.PlateNumber == plate &&
                        e.Status != "Rejected" &&
                        e.ExpenseDate >= day && e.ExpenseDate < next);

                    if (alreadyUsed)
                    {
                        TempData["ErrorMessage"] =
                            $"Vehicle {plate} already has an expense on {day:MMM dd, yyyy}. A vehicle can only be used once per day.";
                        return RedirectToAction(nameof(Submit_Expense));
                    }
                }
                // ────────────────────────────────

                // ───────── BUDGET CHECK ─────────
                var budgetCheck = await _budgetService.CheckAsync(
                    user.Department, model.ExpenseDate, model.Amount);

                if (!budgetCheck.Allowed)
                {
                    TempData["ErrorMessage"] = budgetCheck.Message;
                    return RedirectToAction(nameof(Submit_Expense));
                }
                // ────────────────────────────────

                var expense = new Expense
                {
                    UserId = user.Id,
                    ExpenseType = model.ExpenseType,
                    Amount = model.Amount,
                    ExpenseDate = model.ExpenseDate,
                    PlateNumber = plate,   // normalized, or null when no vehicle chosen
                    Description = model.Description,
                    Status = "Pending",
                    SupervisorStatus = "Pending",
                    DepartmentStatus = "Pending",
                    DateSubmitted = DateTime.Now
                };

                // File Upload (keep as is)...

                _context.Expenses.Add(expense);

                await _auditService.LogAsync("Create", "Expenses",
                    $"Employee {user.FullName ?? user.UserName} submitted expense ₱{model.Amount}...");

                await _context.SaveChangesAsync();

                // Over budget but not blocked, or near the alert threshold
                TempData["SuccessMessage"] = budgetCheck.IsWarningOnly
                    ? $"Expense submitted successfully! Note: {budgetCheck.Message}"
                    : "Expense submitted successfully!";
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;

                TempData["ErrorMessage"] = $"Error: {errorMessage}";

                Console.WriteLine("=== SUBMIT ERROR ===");
                Console.WriteLine(ex.ToString());
            }

            return RedirectToAction(nameof(Submit_Expense));
        }

        // ═══════════════════════════════════════
        // REIMBURSEMENT STATUS
        // ═══════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Reimbursement_Status(
            string? search = null,
            string? status = null,
            string? category = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var mine = _context.Expenses.Where(e => e.UserId == user.Id);

            // ---- Summary cards (not affected by the filters) ----
            var approvedOrPaid = mine.Where(e => ApprovedStatuses.Contains(e.Status) || e.Status == "Reimbursed");
            var rejected = mine.Where(e => e.Status == "Rejected");
            var pending = mine.Where(e => e.Status != "Rejected"
                                          && e.Status != "Reimbursed"
                                          && !ApprovedStatuses.Contains(e.Status));

            ViewBag.TotalApproved = await approvedOrPaid.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            ViewBag.TotalPending = await pending.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            ViewBag.TotalRejected = await rejected.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            ViewBag.TotalSubmitted = await mine.CountAsync();

            // ---- Filtered list ----
            var q = mine;

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(e => (e.Description != null && e.Description.Contains(search)) ||
                                 e.ExpenseType.Contains(search));

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(e => e.ExpenseType == category);

            if (dateFrom.HasValue)
                q = q.Where(e => e.ExpenseDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(e => e.ExpenseDate < dateTo.Value.Date.AddDays(1));

            q = status switch
            {
                "Approved" => q.Where(e => ApprovedStatuses.Contains(e.Status)),
                "Reimbursed" => q.Where(e => e.Status == "Reimbursed"),
                "Rejected" => q.Where(e => e.Status == "Rejected"),
                "Pending" => q.Where(e => e.Status != "Rejected"
                                          && e.Status != "Reimbursed"
                                          && !ApprovedStatuses.Contains(e.Status)),
                _ => q
            };

            var expenses = await q.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            // Payment details (reference no. and date) for expenses that are already paid
            var ids = expenses.Select(e => e.Id).ToList();
            var paymentRows = await _context.Reimbursements
                .Where(r => ids.Contains(r.ExpenseId))
                .ToListAsync();

            ViewBag.Payments = paymentRows
                .GroupBy(r => r.ExpenseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.PaymentDate).First());

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Category = category;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(expenses);
        }

        // ═══════════════════════════════════════
        // PROFILE
        // ═══════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            return View(user);
        }
    }
}