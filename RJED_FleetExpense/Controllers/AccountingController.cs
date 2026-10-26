using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using RJED_FleetExpense.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace RJED_FleetExpense.Controllers
{
    [Authorize(Roles = "Accounting")]
    [Route("Accounting")]
    public class AccountingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditService;
        private readonly IPayMongoCheckoutService _paymongo;

        private static readonly string[] PayableStatuses = { "DepartmentApproved", "ForAccounting" };

        private static readonly string[] PayMongoMethods = { "gcash" };

        public AccountingController(
            ApplicationDbContext context,
            IAuditLogService auditService,
            IPayMongoCheckoutService paymongo)
        {
            _context = context;
            _auditService = auditService;
            _paymongo = paymongo;
        }

        [HttpGet]
        [Route("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.Now;

            // Single "awaiting payment" number (the old "Pending Validation" card is removed)
            ViewBag.ForPayment = await _context.Expenses
                .CountAsync(e => PayableStatuses.Contains(e.Status));

            ViewBag.TotalReimbursed = await _context.Reimbursements
                .Where(r => r.PaymentDate.Month == now.Month &&
                            r.PaymentDate.Year == now.Year)
                .SumAsync(r => (decimal?)r.AmountPaid) ?? 0;

            ViewBag.TotalRejected = await _context.Expenses
                .CountAsync(e => e.Status == "Rejected");

            ViewBag.PendingList = await _context.Database
                .SqlQueryRaw<PendingExpenseDto>(@"
                    SELECT TOP 10
                        e.Id AS ExpenseId,
                        u.UserName AS EmployeeName,
                        ISNULL(c.CategoryName, e.ExpenseType) AS Category,
                        e.Amount,
                        e.ExpenseDate
                    FROM Expenses e
                    LEFT JOIN AspNetUsers u ON u.Id = e.UserId
                    LEFT JOIN Expense_Categories c ON c.category_id = e.CategoryId
                    WHERE e.Status IN ('DepartmentApproved', 'ForAccounting')
                ").ToListAsync();

            int total = await _context.Expenses.CountAsync();
            int reimbursedCount = await _context.Reimbursements.CountAsync();
            int rejectedCount = await _context.Expenses.CountAsync(e => e.Status == "Rejected");
            int pendingCount = await _context.Expenses.CountAsync(e => PayableStatuses.Contains(e.Status));

            ViewBag.ReimbursedPct = total > 0 ? Math.Round((double)reimbursedCount / total * 100) : 0;
            ViewBag.PendingPct = total > 0 ? Math.Round((double)pendingCount / total * 100) : 0;
            ViewBag.RejectedPct = total > 0 ? Math.Round((double)rejectedCount / total * 100) : 0;

            ViewData["Title"] = "Accounting Dashboard";
            return View();
        }

        // ===================== OLD URLS (kept so bookmarks/links don't 404) =====================
        [HttpGet]
        [Route("ForProcessing")]
        public IActionResult ForProcessing() => RedirectToAction(nameof(ProcessReimbursement));

        [HttpGet]
        [Route("PendingPayments")]
        public IActionResult PendingPayments() => RedirectToAction(nameof(ProcessReimbursement));

        // Record Payment was merged into Process Reimbursement (Payment History tab)
        [HttpGet]
        [Route("RecordPayment")]
        public IActionResult RecordPayment() =>
            RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });

        // ===================== PROCESS REIMBURSEMENT (pay + history in one page) =====================

        // The signed-in accountant's Identity user id (a string/GUID), stored on every
        // Reimbursement as ProcessedById.
        private Task<string?> CurrentUserIdAsync() =>
            Task.FromResult(User.FindFirstValue(ClaimTypes.NameIdentifier));

        [HttpGet]
        [Route("ProcessReimbursement")]
        public async Task<IActionResult> ProcessReimbursement(
            string? search = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string tab = "pending")
        {
            var vm = new ProcessReimbursementViewModel
            {
                Search = search?.Trim(),
                DateFrom = dateFrom,
                DateTo = dateTo,
                Tab = tab == "history" ? "history" : "pending"
            };

            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // ---- Summary cards (not affected by the filters) ----
            // Approved by the department head and not paid yet
            var allPayable = _context.Expenses
                .Where(e => PayableStatuses.Contains(e.Status) &&
                            !_context.Reimbursements.Any(r => r.ExpenseId == e.Id));

            vm.PayableCount = await allPayable.CountAsync();
            vm.PayableAmount = await allPayable.SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var paidThisMonth = _context.Reimbursements.Where(r => r.PaymentDate >= monthStart);
            vm.PaidThisMonth = await paidThisMonth.SumAsync(r => (decimal?)r.AmountPaid) ?? 0m;
            vm.PaidThisMonthCount = await paidThisMonth.CountAsync();

            // ---- Awaiting payment ----
            var payable = allPayable;

            if (!string.IsNullOrWhiteSpace(vm.Search))
                payable = payable.Where(e => e.User != null && e.User.UserName.Contains(vm.Search));
            if (dateFrom.HasValue)
                payable = payable.Where(e => e.ExpenseDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                payable = payable.Where(e => e.ExpenseDate < dateTo.Value.Date.AddDays(1));

            vm.Payable = await payable
                .OrderBy(e => e.DateSubmitted)
                .Select(e => new PayableExpenseRow
                {
                    ExpenseId = e.Id,
                    EmployeeName = e.User != null ? e.User.UserName : "Unknown",
                    Category = e.Category != null ? e.Category.CategoryName : e.ExpenseType,
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    DateSubmitted = e.DateSubmitted
                })
                .ToListAsync();

            // ---- Payment history (latest 100) ----
            var history = _context.Reimbursements.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.Search))
                history = history.Where(r =>
                    (r.Expense.User != null && r.Expense.User.UserName.Contains(vm.Search)) ||
                    (r.TransactionRef != null && r.TransactionRef.Contains(vm.Search)));
            if (dateFrom.HasValue)
                history = history.Where(r => r.PaymentDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                history = history.Where(r => r.PaymentDate < dateTo.Value.Date.AddDays(1));

            vm.History = await history
                .OrderByDescending(r => r.PaymentDate)
                .ThenByDescending(r => r.ReimbursementId)
                .Take(100)
                .Select(r => new PaymentHistoryRow
                {
                    ReimbursementId = r.ReimbursementId,
                    ExpenseId = r.ExpenseId,
                    EmployeeName = r.Expense.User != null ? r.Expense.User.UserName : "Unknown",
                    Category = r.Expense.Category != null ? r.Expense.Category.CategoryName : r.Expense.ExpenseType,
                    TransactionRef = r.TransactionRef,
                    AmountPaid = r.AmountPaid,
                    PaymentDate = r.PaymentDate,
                    ProcessedByName = r.ProcessedBy != null
                        ? (r.ProcessedBy.FullName ?? r.ProcessedBy.Email)
                        : "—"
                })
                .ToListAsync();

            return View(vm);
        }

        // ---- Pay one expense through PayMongo ----
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("ProcessReimbursement")]
        public async Task<IActionResult> ProcessReimbursement(int expenseId, decimal amountPaid)
        {
            var userId = await CurrentUserIdAsync();
            if (userId == null)
            {
                TempData["Error"] = "Could not identify the signed-in user. Please sign in again.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            var expense = await _context.Expenses
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == expenseId);
            if (expense == null)
            {
                TempData["Error"] = "Expense not found.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            if (!PayableStatuses.Contains(expense.Status))
            {
                TempData["Error"] = $"Only department-approved expenses can be reimbursed. Current status: {expense.Status}";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            if (amountPaid != expense.Amount)
            {
                TempData["Error"] = $"Amount paid must match the claim amount (₱{expense.Amount:N2}).";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            if (await _context.Reimbursements.AnyAsync(r => r.ExpenseId == expenseId))
            {
                TempData["Error"] = "This expense has already been reimbursed.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            var ids = expenseId.ToString();
            var successUrl = Url.Action(nameof(PayMongoReturn), "Accounting", new { ids }, Request.Scheme)!;
            var cancelUrl = Url.Action(nameof(ProcessReimbursement), "Accounting", null, Request.Scheme)!;

            var created = await _paymongo.CreateAsync(
                expense.Amount, "Expense reimbursement",
                $"Reimbursement for Expense #{expenseId}", $"EXP-{expenseId}",
                successUrl, cancelUrl, PayMongoMethods,
                expense.User?.FullName ?? expense.User?.UserName,
                expense.User?.Email);

            if (!created.Success)
            {
                TempData["Error"] = "PayMongo could not start the payment. " + created.Error;
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            TempData[$"pm_{ids}"] = created.SessionId;
            return Redirect(created.CheckoutUrl!);
        }

        // ---- Pay several expenses: one PayMongo checkout for the total ----
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("BulkReimburse")]
        public async Task<IActionResult> BulkReimburse(int[] expenseIds)
        {
            var userId = await CurrentUserIdAsync();
            if (userId == null)
            {
                TempData["Error"] = "Could not identify the signed-in user. Please sign in again.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            if (expenseIds == null || expenseIds.Length == 0)
            {
                TempData["Error"] = "Select at least one expense to pay.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            expenseIds = expenseIds.Distinct().ToArray();

            var alreadyPaid = await _context.Reimbursements
                .Where(r => expenseIds.Contains(r.ExpenseId))
                .Select(r => r.ExpenseId)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => expenseIds.Contains(e.Id) && PayableStatuses.Contains(e.Status))
                .ToListAsync();
            expenses = expenses.Where(e => !alreadyPaid.Contains(e.Id)).ToList();

            if (expenses.Count == 0)
            {
                TempData["Error"] = "None of the selected expenses can be reimbursed (already paid or no longer approved).";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            var ids = string.Join(",", expenses.Select(e => e.Id).OrderBy(i => i));
            var grandTotal = expenses.Sum(e => e.Amount);
            var successUrl = Url.Action(nameof(PayMongoReturn), "Accounting", new { ids }, Request.Scheme)!;
            var cancelUrl = Url.Action(nameof(ProcessReimbursement), "Accounting", null, Request.Scheme)!;

            // Several employees share one checkout, so the signed-in accountant's details are used.
            var created = await _paymongo.CreateAsync(
                grandTotal, $"{expenses.Count} expense reimbursements",
                $"Bulk reimbursement for Expense IDs {ids}", $"BULK-{DateTime.Now:yyyyMMddHHmmss}",
                successUrl, cancelUrl, PayMongoMethods,
                "RJED Accounting",
                User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name);

            if (!created.Success)
            {
                TempData["Error"] = "PayMongo could not start the payment. " + created.Error;
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            TempData[$"pm_{ids}"] = created.SessionId;
            return Redirect(created.CheckoutUrl!);
        }

        // ---- PayMongo sends the user back here after the hosted checkout ----
        [HttpGet]
        [Route("PayMongoReturn")]
        public async Task<IActionResult> PayMongoReturn(string ids)
        {
            var key = $"pm_{ids}";
            var sessionId = TempData.Peek(key) as string;
            var userId = await CurrentUserIdAsync();

            if (userId == null || string.IsNullOrEmpty(sessionId))
            {
                TempData["Error"] = "Could not confirm the PayMongo payment (session expired). Nothing was recorded.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            // Never trust the redirect alone: ask PayMongo if it was really paid.
            var status = await _paymongo.GetStatusAsync(sessionId);
            if (!status.Success)
            {
                TempData["Error"] = "Could not verify the payment with PayMongo. " + status.Error;
                return RedirectToAction(nameof(ProcessReimbursement));
            }
            if (!status.Paid)
            {
                TempData["Warning"] = "The PayMongo payment was not completed. Nothing was recorded.";
                return RedirectToAction(nameof(ProcessReimbursement));
            }

            var expenseIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => int.TryParse(s, out var n) ? n : 0)
                                .Where(n => n > 0).ToArray();

            var alreadyPaid = await _context.Reimbursements
                .Where(r => expenseIds.Contains(r.ExpenseId))
                .Select(r => r.ExpenseId)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => expenseIds.Contains(e.Id) && PayableStatuses.Contains(e.Status))
                .ToListAsync();
            expenses = expenses.Where(e => !alreadyPaid.Contains(e.Id)).ToList();

            if (expenses.Count == 0)
            {
                TempData.Remove(key);
                TempData["Warning"] = "These expenses were already recorded as paid.";
                return RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });
            }

            var reference = status.PaymentId ?? sessionId;
            if (reference.Length > 50) reference = reference.Substring(0, 50);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var e in expenses)
                {
                    _context.Reimbursements.Add(new Reimbursement
                    {
                        ExpenseId = e.Id,
                        ProcessedById = userId,
                        TransactionRef = reference,
                        AmountPaid = e.Amount,
                        PaymentDate = DateTime.Now
                    });
                    e.Status = "Reimbursed";
                }
                await _context.SaveChangesAsync();

                foreach (var e in expenses)
                {
                    await _auditService.LogAsync(
                        "Reimbursement",
                        "Reimbursements",
                        $"Processed reimbursement for Expense ID {e.Id}. Amount: ₱{e.Amount:N2}. Ref: {reference} (PayMongo)");
                }

                await transaction.CommitAsync();
                TempData.Remove(key);
                TempData["Success"] = $"PayMongo payment confirmed: {expenses.Count} expense(s), ₱{expenses.Sum(e => e.Amount):N2} (Ref: {reference}).";
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "The payment succeeded at PayMongo but saving it failed. Do not pay again. Reference: " + reference;
            }

            return RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });
        }

        // ---- Correct the reference number or date of a payment already made ----
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("UpdatePayment")]
        public async Task<IActionResult> UpdatePayment(int reimbursementId, string transactionRef, DateTime paymentDate)
        {
            transactionRef = (transactionRef ?? "").Trim();
            if (transactionRef.Length == 0 || transactionRef.Length > 50)
            {
                TempData["Error"] = "Enter a reference number of up to 50 characters.";
                return RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });
            }

            var reimbursement = await _context.Reimbursements.FindAsync(reimbursementId);
            if (reimbursement == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });
            }

            var oldRef = reimbursement.TransactionRef;
            reimbursement.TransactionRef = transactionRef;
            reimbursement.PaymentDate = paymentDate;
            await _context.SaveChangesAsync();

            // Same action name as before so the Financial Audit Log cards keep counting it
            await _auditService.LogAsync(
                "Payment Recorded",
                "Reimbursements",
                $"Updated payment for Reimbursement ID {reimbursementId}. Ref: {oldRef} -> {transactionRef}");

            TempData["Success"] = "Payment details updated.";
            return RedirectToAction(nameof(ProcessReimbursement), new { tab = "history" });
        }

        // ===================== EXPENSE REPORT (report + summary in one) =====================

        private IQueryable<Expense> ReportQuery(
            DateTime? dateFrom, DateTime? dateTo, string? category, string? status, string? department)
        {
            var q = _context.Expenses
                .Include(e => e.User)
                .Include(e => e.Category)
                .AsQueryable();

            if (dateFrom.HasValue) q = q.Where(e => e.ExpenseDate >= dateFrom.Value.Date);
            if (dateTo.HasValue) q = q.Where(e => e.ExpenseDate < dateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(e => (e.Category != null ? e.Category.CategoryName : e.ExpenseType) == category);

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(e => e.Status == status);

            // Expense.User.Department is a plain string
            if (!string.IsNullOrWhiteSpace(department))
                q = q.Where(e => e.User != null && e.User.Department == department);

            return q;
        }

        [HttpGet]
        [Route("GenerateExpenseReport")]
        public async Task<IActionResult> GenerateExpenseReport(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? category = null,
            string? status = null,
            string? department = null)
        {
            var today = DateTime.Today;

            var vm = new ExpenseReportViewModel
            {
                // The report only runs after the form is submitted
                Generated = Request.Query.ContainsKey("dateFrom"),
                DateFrom = dateFrom ?? new DateTime(today.Year, today.Month, 1),
                DateTo = dateTo ?? today,
                Category = category,
                Status = status,
                Department = department
            };

            vm.CategoryOptions = await _context.ExpenseCategories
                .Select(c => c.CategoryName).OrderBy(n => n).ToListAsync();

            vm.DepartmentOptions = await _context.Expenses
                .Where(e => e.User != null && e.User.Department != null && e.User.Department != "")
                .Select(e => e.User.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            if (!vm.Generated)
                return View(vm);

            vm.Rows = await ReportQuery(vm.DateFrom, vm.DateTo, category, status, department)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => new ExpenseReportRow
                {
                    ExpenseId = e.Id,
                    EmployeeName = e.User != null ? e.User.UserName : "Unknown",
                    Department = e.User != null && e.User.Department != null ? e.User.Department : "—",
                    Category = e.Category != null ? e.Category.CategoryName : e.ExpenseType,
                    Description = e.Description ?? "",   // ASSUMPTION: Expense has Description
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    Status = e.Status
                })
                .ToListAsync();

            BuildSummary(vm);
            return View(vm);
        }

        private static void BuildSummary(ExpenseReportViewModel vm)
        {
            var rows = vm.Rows;
            if (rows.Count == 0) return;

            decimal grand = vm.GrandTotal;
            double Pct(decimal part) => grand > 0 ? Math.Round((double)(part / grand) * 100, 1) : 0;

            vm.ByCategory = rows
                .GroupBy(r => r.Category)
                .Select(g => new BreakdownItem
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(r => r.Amount),
                    Pct = Pct(g.Sum(r => r.Amount))
                })
                .OrderByDescending(x => x.Total).ToList();

            vm.ByDepartment = rows
                .GroupBy(r => r.Department)
                .Select(g => new BreakdownItem { Name = g.Key, Count = g.Count(), Total = g.Sum(r => r.Amount) })
                .OrderByDescending(x => x.Total).ToList();

            vm.TopClaimants = rows
                .GroupBy(r => r.EmployeeName)
                .Select(g => new BreakdownItem
                {
                    Name = g.Key,
                    Sub = g.First().Department,
                    Count = g.Count(),
                    Total = g.Sum(r => r.Amount)
                })
                .OrderByDescending(x => x.Total).Take(5).ToList();

            // Trend: by day for ranges up to ~2 months, otherwise by month
            bool daily = (vm.DateTo!.Value - vm.DateFrom!.Value).TotalDays <= 62;
            var trend = rows
                .GroupBy(r => daily ? r.ExpenseDate.Date : new DateTime(r.ExpenseDate.Year, r.ExpenseDate.Month, 1))
                .OrderBy(g => g.Key)
                .ToList();

            vm.TrendLabels = trend.Select(g => g.Key.ToString(daily ? "MMM dd" : "MMM yyyy", CultureInfo.InvariantCulture)).ToList();
            vm.TrendData = trend.Select(g => g.Sum(r => r.Amount)).ToList();
        }

        [HttpGet]
        [Route("ExportExpenseReport")]
        public async Task<IActionResult> ExportExpenseReport(
            DateTime? dateFrom = null, DateTime? dateTo = null,
            string? category = null, string? status = null, string? department = null)
        {
            var items = await ReportQuery(dateFrom, dateTo, category, status, department)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            static string Csv(string? v)
            {
                v ??= "";
                if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v;   // spreadsheet formula injection
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Date,Employee,Department,Category,Description,Amount,Status");
            foreach (var e in items)
            {
                sb.AppendLine(string.Join(",",
                    Csv(e.ExpenseDate.ToString("yyyy-MM-dd")),
                    Csv(e.User?.UserName),
                    Csv(e.User?.Department),
                    Csv(e.Category?.CategoryName ?? e.ExpenseType),
                    Csv(e.Description),
                    e.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    Csv(e.Status)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"expense-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        // Old Expense Summary URL now lands on the merged page
        [HttpGet]
        [Route("ExpenseSummary")]
        public IActionResult ExpenseSummary() => RedirectToAction(nameof(GenerateExpenseReport));

        // ===================== FINANCIAL AUDIT LOG =====================
        // Only entries about money: expenses and reimbursements.
        private static readonly string[] FinancialTables = { "Expenses", "Reimbursements" };

        private IQueryable<AuditLog> FinancialLogQuery(
            string search, string actionType, string tableAffected, string dateFrom, string dateTo)
        {
            // NOTE: assumes DbSet<AuditLog> is named AuditLogs on ApplicationDbContext
            var query = _context.AuditLogs
                .Include(l => l.ApplicationUser)
                .Where(l => FinancialTables.Contains(l.TableAffected));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l =>
                    l.Details.Contains(search) ||
                    l.ActionType.Contains(search) ||
                    (l.ApplicationUser != null && l.ApplicationUser.Email.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(actionType))
                query = query.Where(l => l.ActionType == actionType);

            if (!string.IsNullOrWhiteSpace(tableAffected))
                query = query.Where(l => l.TableAffected == tableAffected);

            if (DateTime.TryParse(dateFrom, out var from))
                query = query.Where(l => l.Timestamp >= from.Date);

            if (DateTime.TryParse(dateTo, out var to))
                query = query.Where(l => l.Timestamp < to.Date.AddDays(1));

            return query;
        }

        [HttpGet]
        [Route("FinancialAuditLog")]
        public async Task<IActionResult> FinancialAuditLog(
            string? search = null,
            string? actionType = null,
            string? tableAffected = null,
            string? dateFrom = null,
            string? dateTo = null,
            int page = 1)
        {
            const int pageSize = 15;
            if (page < 1) page = 1;

            var query = FinancialLogQuery(search, actionType, tableAffected, dateFrom, dateTo);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Summary cards (all financial entries, not affected by the filters)
            var allFinancial = _context.AuditLogs.Where(l => FinancialTables.Contains(l.TableAffected));
            var todayStart = DateTime.UtcNow.Date;

            ViewBag.TotalEntries = await allFinancial.CountAsync();
            ViewBag.ReimbursementsProcessed = await allFinancial.CountAsync(l => l.ActionType == "Reimbursement");
            ViewBag.PaymentsUpdated = await allFinancial.CountAsync(l => l.ActionType == "Payment Recorded");
            ViewBag.ActivityToday = await allFinancial.CountAsync(l => l.Timestamp >= todayStart);
            ViewBag.TotalPaid = await _context.Reimbursements.SumAsync(r => (decimal?)r.AmountPaid) ?? 0m;

            // Filter dropdowns
            ViewBag.Tables = FinancialTables.ToList();
            ViewBag.ActionTypes = await allFinancial
                .Select(l => l.ActionType)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.ActionType = actionType;
            ViewBag.TableAffected = tableAffected;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = total;

            ViewData["Title"] = "Financial Audit Log";
            return View(logs);
        }

        [HttpGet]
        [Route("ExportFinancialAuditLog")]
        public async Task<IActionResult> ExportFinancialAuditLog(
            string? search = null,
            string? actionType = null,
            string? tableAffected = null,
            string? dateFrom = null,
            string? dateTo = null)
        {
            var logs = await FinancialLogQuery(search, actionType, tableAffected, dateFrom, dateTo)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();

            static string Csv(string? v)
            {
                v ??= "";
                // Neutralise spreadsheet formula injection and escape quotes
                if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v;
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,User,Action,Table,Details");
            foreach (var l in logs)
            {
                sb.AppendLine(string.Join(",",
                    Csv(l.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    Csv(l.ApplicationUser?.Email ?? "Unknown"),
                    Csv(l.ActionType),
                    Csv(l.TableAffected),
                    Csv(l.Details)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"financial-audit-log-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }
    }

    public class PendingExpenseDto
    {
        public int ExpenseId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
    }
}