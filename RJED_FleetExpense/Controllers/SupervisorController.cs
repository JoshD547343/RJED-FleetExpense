using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using RJED.ViewModels.Supervisor;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RJED_FleetExpense.Controllers
{
    [Authorize(Roles = "Supervisor")]
    [Route("Supervisor")]
    public class SupervisorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SupervisorController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ===============================
        // DASHBOARD
        // ===============================
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
                .SumAsync(e => e.Amount);

            ViewBag.TotalApproved = await _context.Expenses
                .Where(e => e.Status == "SupervisorApproved" ||
                            e.Status == "DepartmentApproved" ||
                            e.Status == "ForAccounting" ||
                            e.Status == "Reimbursed")
                .SumAsync(e => e.Amount);

            ViewBag.PendingCount = await _context.Expenses
                .CountAsync(e => e.Status == "Pending");

            ViewBag.RejectedCount = await _context.Expenses
                .CountAsync(e => e.Status == "Rejected");

            ViewBag.CurrentUserName = user.FullName ?? user.UserName ?? "Supervisor";
            ViewBag.CurrentUserEmail = user.Email ?? "—";

            var teamActivity = await _context.Expenses
                .Include(e => e.User)
                .GroupBy(e => new { e.UserId, e.User.UserName })
                .Select(g => new
                {
                    Name = g.Key.UserName ?? "Unknown",
                    Initials = g.Key.UserName != null
                        ? g.Key.UserName.Substring(0, 1).ToUpper()
                        : "?",
                    ExpenseCount = g.Count(),
                    TotalAmount = g.Sum(e => e.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(5)
                .ToListAsync();

            ViewBag.TeamActivity = teamActivity;

            var pendingExpenses = await _context.Expenses
                .Include(e => e.User)
                .Where(e => e.Status == "Pending")
                .OrderByDescending(e => e.DateSubmitted)
                .Take(5)
                .ToListAsync();

            ViewData["Title"] = "Supervisor Dashboard";
            return View("Dashboard", pendingExpenses);
        }

        // ===============================
        // PENDING EXPENSES (For Supervisor)
        // ===============================
        [HttpGet]
        [Route("PendingExpense")]
        public async Task<IActionResult> PendingExpenses(
            string? search, string? dateFrom, string? dateTo)
        {
            var query = _context.Expenses
                .Include(e => e.User)
                .Where(e => e.Status == "Pending")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e =>
                    e.Description.Contains(search) ||
                    e.ExpenseType.Contains(search));

            if (!string.IsNullOrEmpty(dateFrom) &&
                DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from);

            if (!string.IsNullOrEmpty(dateTo) &&
                DateTime.TryParse(dateTo, out var to))
                query = query.Where(e => e.ExpenseDate <= to);

            var expenses = await query
                .OrderByDescending(e => e.DateSubmitted)
                .ToListAsync();

            var vm = new PendingExpensesPageViewModel
            {
                Expenses = expenses,
                PendingCount = expenses.Count,
                SearchTerm = search,
                DateFrom = string.IsNullOrEmpty(dateFrom) ? null
                               : DateTime.TryParse(dateFrom, out var df) ? df : null,
                DateTo = string.IsNullOrEmpty(dateTo) ? null
                               : DateTime.TryParse(dateTo, out var dt) ? dt : null
            };

            ViewData["Title"] = "Pending Expenses";
            return View("PendingExpense", vm);
        }

        // ===============================
        // APPROVED EXPENSES (By Supervisor & Above)
        // ===============================
        [HttpGet]
        [Route("Approve")]
        public async Task<IActionResult> ApprovedExpenses(
            string? search, string? dateFrom, string? dateTo, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Expenses
                .Include(e => e.User)
                .Where(e => e.Status == "SupervisorApproved" ||
                            e.Status == "DepartmentApproved" ||
                            e.Status == "ForAccounting" ||
                            e.Status == "Reimbursed")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e =>
                    e.Description.Contains(search) ||
                    e.ExpenseType.Contains(search) ||
                    e.User.UserName.Contains(search));

            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var f))
            {
                from = f;
                query = query.Where(e => e.ExpenseDate >= f);
            }
            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var t))
            {
                to = t;
                query = query.Where(e => e.ExpenseDate <= t);
            }

            var totalCount = await query.CountAsync();
            var totalAmount = await query.SumAsync(e => e.Amount);

            var expenses = await query
                .OrderByDescending(e => e.DateSubmitted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new ExpenseRowViewModel
                {
                    Id = e.Id,
                    EmployeeName = e.User.FullName ?? e.User.UserName ?? "Unknown",
                    Department = e.User.Department ?? "—",
                    ExpenseType = e.ExpenseType,
                    Amount = e.Amount,
                    SubmittedDate = e.DateSubmitted,
                    ApprovedDate = e.SupervisorApprovedDate,
                    SupervisorRemarks = e.SupervisorRemarks,
                    IsForwarded = e.IsForwarded ?? false,
                    ForwardedTo = e.ForwardedTo
                })
                .ToListAsync();

            var vm = new ApprovedExpensesPageViewModel
            {
                Expenses = expenses,
                TotalApprovedAmount = totalAmount,
                TotalCount = totalCount,
                SearchTerm = search,
                DateFrom = from,
                DateTo = to,
                Page = page,
                PageSize = pageSize
            };

            ViewData["Title"] = "Approved Expenses";
            return View("Approve", vm);
        }

        // ===============================
        // REJECTED EXPENSES
        // ===============================
        [HttpGet]
        [Route("Reject")]
        public async Task<IActionResult> RejectedExpenses(
            string? search, string? dateFrom, string? dateTo,
            string? reason, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Expenses
                .Include(e => e.User)
                .Where(e => e.Status == "Rejected")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e =>
                    e.Description.Contains(search) ||
                    e.ExpenseType.Contains(search) ||
                    e.User.UserName.Contains(search));

            if (!string.IsNullOrEmpty(reason))
                query = query.Where(e => e.RejectionReason == reason);

            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var f))
            {
                from = f;
                query = query.Where(e => e.ExpenseDate >= f);
            }
            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var t))
            {
                to = t;
                query = query.Where(e => e.ExpenseDate <= t);
            }

            var totalCount = await query.CountAsync();
            var allReasons = await _context.Expenses
                .Where(e => e.Status == "Rejected" && e.RejectionReason != null)
                .Select(e => e.RejectionReason!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            var expenses = await query
                .OrderByDescending(e => e.DateSubmitted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new ExpenseRowViewModel
                {
                    Id = e.Id,
                    EmployeeName = e.User.FullName ?? e.User.UserName ?? "Unknown",
                    Department = e.User.Department ?? "—",
                    ExpenseType = e.ExpenseType,
                    Amount = e.Amount,
                    SubmittedDate = e.DateSubmitted,
                    RejectedDate = e.SupervisorRejectedDate,
                    SupervisorRemarks = e.SupervisorRemarks,
                    RejectionReason = e.RejectionReason
                })
                .ToListAsync();

            var vm = new RejectedExpensesPageViewModel
            {
                Expenses = expenses,
                TotalCount = totalCount,
                SearchTerm = search,
                DateFrom = from,
                DateTo = to,
                ReasonFilter = reason,
                RejectionReasons = allReasons,
                Page = page,
                PageSize = pageSize
            };

            ViewData["Title"] = "Rejected Expenses";
            return View("Reject", vm);
        }

        // ===============================
        // EXPENSE DETAILS
        // ===============================
        [HttpGet]
        [Route("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null) return NotFound();

            // Vehicle type for the plate the employee picked
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

        // ===============================
        // APPROVE EXPENSE → Forward to Department Head
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Approve/{id}")]
        public async Task<IActionResult> ApproveExpense(int id, string? remarks)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            if (expense.Status != "Pending")
            {
                TempData["Error"] = "This expense cannot be approved — it is no longer pending.";
                return RedirectToAction("PendingExpenses");
            }

            // === Key Change: Multi-level flow ===
            expense.Status = "SupervisorApproved";
            expense.SupervisorStatus = "Approved";
            expense.SupervisorApprovedDate = DateTime.Now;
            expense.SupervisorRemarks = remarks?.Trim();
            expense.IsForwarded = true;
            expense.ForwardedTo = "Department Head";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Expense approved and forwarded to Department Head.";
            return RedirectToAction("PendingExpenses");
        }

        // ===============================
        // REJECT EXPENSE
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Reject/{id}")]
        public async Task<IActionResult> RejectExpense(
            int id,
            string? remarks,
            string? rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(remarks))
            {
                TempData["Error"] = "A rejection reason is required.";
                return RedirectToAction("PendingExpenses");
            }

            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            if (expense.Status != "Pending")
            {
                TempData["Error"] = "This expense cannot be rejected — it is no longer pending.";
                return RedirectToAction("PendingExpenses");
            }

            expense.Status = "Rejected";
            expense.SupervisorStatus = "Rejected";
            expense.SupervisorRejectedDate = DateTime.Now;
            expense.SupervisorRemarks = remarks.Trim();
            expense.RejectionReason = rejectionReason?.Trim();

            await _context.SaveChangesAsync();

            TempData["Warning"] = "Expense has been rejected.";
            return RedirectToAction("PendingExpenses");
        }

        // ===============================
        // MY TEAM (Kept Original)
        // ===============================
        [HttpGet]
        [Route("MyTeam")]
        public async Task<IActionResult> MyTeam(
            string? search,
            string? department)
        {
            var teamMembers = await _userManager.GetUsersInRoleAsync("Employee");
            var filtered = teamMembers.AsEnumerable();

            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(u =>
                    (u.FullName ?? u.UserName ?? "")
                        .Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (u.Email ?? "")
                        .Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(department))
            {
                filtered = filtered.Where(u =>
                    (u.Department ?? "")
                        .Equals(department, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            var userIds = filteredList.Select(u => u.Id).ToList();

            var allExpenses = await _context.Expenses
                .Where(e => userIds.Contains(e.UserId))
                .ToListAsync();

            var memberVms = filteredList.Select(u =>
            {
                var userExpenses = allExpenses.Where(e => e.UserId == u.Id).ToList();
                var name = u.FullName ?? u.UserName ?? "Unknown";
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var initials = parts.Length >= 2
                    ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                    : name[0].ToString().ToUpper();

                return new TeamMemberViewModel
                {
                    UserId = u.Id,
                    FullName = name,
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber,
                    Department = u.Department,
                    AvatarInitials = initials,
                    TotalSubmitted = userExpenses.Count,
                    PendingCount = userExpenses.Count(e => e.Status == "Pending"),
                    ApprovedCount = userExpenses.Count(e => e.Status == "SupervisorApproved" ||
                                                           e.Status == "DepartmentApproved"),
                    RejectedCount = userExpenses.Count(e => e.Status == "Rejected"),
                    TotalAmountSubmitted = userExpenses.Sum(e => e.Amount),
                    LastSubmittedDate = userExpenses.Max(e => (DateTime?)e.DateSubmitted)
                };
            }).ToList();

            var mostActive = memberVms.OrderByDescending(m => m.TotalSubmitted).FirstOrDefault();
            var departments = teamMembers
                .Where(u => !string.IsNullOrEmpty(u.Department))
                .Select(u => u.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var vm = new MyTeamPageViewModel
            {
                TeamMembers = memberVms,
                TotalTeamMembers = teamMembers.Count,
                TotalPendingAcrossTeam = memberVms.Sum(m => m.PendingCount),
                Departments = departments,
                SearchTerm = search,
                DepartmentFilter = department,
                MostActiveEmployeeName = mostActive?.FullName,
                MostActiveEmployeeCount = mostActive?.TotalSubmitted ?? 0
            };

            ViewData["Title"] = "My Team";
            return View("MyTeam", vm);
        }
    }
}