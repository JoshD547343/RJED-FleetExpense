using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using System.Text;
using System.Text.RegularExpressions;
using RJED_FleetExpense.Services;

namespace RJED_FleetExpense.Controllers
{
    [Authorize(Roles = "Admin, Super_Admin")]
    [Route("Admin/Dashboard")]
    public class AdminDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly BudgetService _budgetService;

        private static readonly string[] AllowedVehicleStatuses = { "Active", "Maintenance", "Retired" };

        public AdminDashboardController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            BudgetService budgetService)
        {
            _userManager = userManager;
            _context = context;
            _budgetService = budgetService;
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

            ViewBag.TotalUsers = await _userManager.Users.CountAsync();

            ViewBag.TotalExpensesThisMonth = await _context.Expenses
                .Where(e => e.ExpenseDate.Month == DateTime.Now.Month &&
                            e.ExpenseDate.Year == DateTime.Now.Year)
                .SumAsync(e => e.Amount);

            ViewBag.PendingApprovals = await _context.Expenses
                .CountAsync(e => e.Status == "Pending");

            ViewBag.ActiveDepartments = await _userManager.Users
                .Where(u => u.Department != null)
                .Select(u => u.Department)
                .Distinct()
                .CountAsync();

            ViewBag.TotalApproved = await _context.Expenses
                .Where(e => e.Status == "Approved")
                .SumAsync(e => e.Amount);

            ViewBag.TotalPending = await _context.Expenses
                .Where(e => e.Status == "Pending")
                .SumAsync(e => e.Amount);

            ViewBag.TotalRejected = await _context.Expenses
                .Where(e => e.Status == "Rejected")
                .SumAsync(e => e.Amount);

            ViewBag.RecentExpenses = await _context.Expenses
                .Include(e => e.User)
                .OrderByDescending(e => e.DateSubmitted)
                .Take(8)
                .ToListAsync();

            ViewBag.RecentUsers = await _userManager.Users
                .OrderByDescending(u => u.Id)
                .Take(6)
                .ToListAsync();

            ViewData["Title"] = "Admin Dashboard";
            return View();
        }

        // ===============================
        // MANAGE USERS — LIST
        // ===============================
        [HttpGet]
        [Route("ManageUser")]
        public async Task<IActionResult> ManageUser()
        {
            var users = await _userManager.Users.ToListAsync();

            var userRoles = new Dictionary<string, IList<string>>();
            foreach (var u in users)
                userRoles[u.Id] = await _userManager.GetRolesAsync(u);

            ViewBag.UserRoles = userRoles;
            ViewData["Title"] = "Manage Users & Roles";
            return View(users);
        }

        // ===============================
        // CREATE USER
        // ===============================
        [HttpPost]
        [Route("ManageUser/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            string fullName,
            string email,
            string role,
            string department,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(role) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "All fields are required.";
                return RedirectToAction("ManageUser");
            }

            if (password != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return RedirectToAction("ManageUser");
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Error"] = $"A user with the email '{email}' already exists.";
                return RedirectToAction("ManageUser");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Department = department,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction("ManageUser");
            }

            if (!string.IsNullOrWhiteSpace(role))
                await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = $"User '{fullName}' was created successfully.";
            return RedirectToAction("ManageUser");
        }

        // ===============================
        // EDIT USER
        // ===============================
        [HttpPost]
        [Route("ManageUser/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(
            string userId,
            string fullName,
            string role,
            string department,
            string status)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("ManageUser");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUser");
            }

            user.FullName = fullName;
            user.Department = department;
            user.IsActive = status == "Active";

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                return RedirectToAction("ManageUser");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrWhiteSpace(role))
                await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = $"User '{fullName}' was updated successfully.";
            return RedirectToAction("ManageUser");
        }

        // ===============================
        // DELETE USER
        // ===============================
        [HttpPost]
        [Route("ManageUser/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("ManageUser");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUser");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == user.Id)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("ManageUser");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction("ManageUser");
            }

            TempData["Success"] = $"User '{user.FullName ?? user.Email}' was deleted.";
            return RedirectToAction("ManageUser");
        }
        // ===============================
        // ARCHIVE USER
        // ===============================
        [HttpPost]
        [Route("ManageUser/Archive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("ManageUser");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUser");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == user.Id)
            {
                TempData["Error"] = "You cannot archive your own account.";
                return RedirectToAction("ManageUser");
            }

            if (user.IsActive)
            {
                TempData["Error"] = "Only inactive users can be archived. Set the user to Inactive first.";
                return RedirectToAction("ManageUser");
            }

            user.IsArchived = true;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction("ManageUser");
            }

            TempData["Success"] = $"User '{user.FullName ?? user.Email}' was archived.";
            return RedirectToAction("ManageUser");
        }

        // ===============================
        // RESTORE USER
        // ===============================
        [HttpPost]
        [Route("ManageUser/Restore")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Error"] = "Invalid user.";
                return RedirectToAction("ManageUser");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUser");
            }

            user.IsArchived = false;
            user.IsActive = false; // comes back as Inactive; admin re-activates via Edit

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction("ManageUser");
            }

            TempData["Success"] = $"User '{user.FullName ?? user.Email}' was restored. Set them to Active to allow sign-in.";
            return RedirectToAction("ManageUser");
        }

        // ===============================
        // AUDIT LOGS
        // ===============================
        [HttpGet]
        [Route("AuditLogs")]
        public async Task<IActionResult> AuditLogs(
            string? search,
            string? actionType,
            string? tableAffected,
            string? dateFrom,
            string? dateTo,
            int page = 1)
        {
            const int pageSize = 20;

            try
            {
                var logs = await _context.AuditLogs
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                var userIds = logs
                    .Where(l => !string.IsNullOrEmpty(l.UserId))
                    .Select(l => l.UserId!)
                    .Distinct()
                    .ToList();

                List<UserEmailDto> users = new();
                if (userIds.Any())
                {
                    var idList = string.Join(",", userIds.Select(id => $"'{id}'"));
                    users = await _context.Database
                        .SqlQueryRaw<UserEmailDto>(
                            $"SELECT Id, Email FROM AspNetUsers WHERE Id IN ({idList})")
                        .ToListAsync();
                }

                // Search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    var matchingUserIds = users
                        .Where(u => u.Email != null && u.Email.ToLower().Contains(s))
                        .Select(u => u.Id)
                        .ToHashSet();

                    logs = logs.Where(l =>
                        (l.UserId != null && matchingUserIds.Contains(l.UserId)) ||
                        (l.ActionType != null && l.ActionType.ToLower().Contains(s)) ||
                        (l.TableAffected != null && l.TableAffected.ToLower().Contains(s)) ||
                        (l.Details != null && l.Details.ToLower().Contains(s))
                    ).ToList();
                }

                // Action Type filter
                if (!string.IsNullOrWhiteSpace(actionType))
                {
                    var a = actionType.Trim().ToLower();
                    logs = logs.Where(l =>
                        l.ActionType != null && l.ActionType.ToLower().Contains(a)
                    ).ToList();
                }

                // Table Affected filter
                if (!string.IsNullOrWhiteSpace(tableAffected))
                {
                    logs = logs.Where(l =>
                        l.TableAffected != null && l.TableAffected == tableAffected
                    ).ToList();
                }

                // Date From filter
                if (!string.IsNullOrWhiteSpace(dateFrom) &&
                    DateTime.TryParse(dateFrom, out var fromDate))
                {
                    logs = logs.Where(l => l.Timestamp >= fromDate).ToList();
                }

                // Date To filter
                if (!string.IsNullOrWhiteSpace(dateTo) &&
                    DateTime.TryParse(dateTo, out var toDate))
                {
                    logs = logs.Where(l => l.Timestamp <= toDate.AddDays(1)).ToList();
                }

                // Get distinct tables for dropdown
                var allTables = await _context.AuditLogs
                    .Where(l => l.TableAffected != null)
                    .Select(l => l.TableAffected!)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                var total = logs.Count;
                var pagedLogs = logs
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Attach emails
                var userDict = users.ToDictionary(u => u.Id, u => u.Email);
                foreach (var log in pagedLogs)
                {
                    if (log.UserId != null && userDict.TryGetValue(log.UserId, out var email))
                        log.ApplicationUser = new ApplicationUser { Email = email };
                }

                ViewBag.Search = search;
                ViewBag.ActionType = actionType;
                ViewBag.TableAffected = tableAffected;
                ViewBag.DateFrom = dateFrom;
                ViewBag.DateTo = dateTo;
                ViewBag.Tables = allTables;
                ViewBag.Page = page;
                ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
                ViewBag.Total = total;
                ViewData["Title"] = "Audit Logs";

                return View(pagedLogs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditLogs Error] {ex.Message}");
                Console.WriteLine($"[AuditLogs Inner] {ex.InnerException?.Message}");

                TempData["Error"] = "Failed to load audit logs. Please try again.";

                ViewBag.Search = search;
                ViewBag.ActionType = actionType;
                ViewBag.TableAffected = tableAffected;
                ViewBag.DateFrom = dateFrom;
                ViewBag.DateTo = dateTo;
                ViewBag.Tables = new List<string>();
                ViewBag.Page = 1;
                ViewBag.TotalPages = 1;
                ViewBag.Total = 0;
                ViewData["Title"] = "Audit Logs";

                return View(new List<AuditLog>());
            }
        }

        // ===============================
        // EXPORT AUDIT LOGS
        // ===============================
        [HttpGet]
        [Route("AuditLogs/Export")]
        public async Task<IActionResult> ExportAuditLogs(
            string? search,
            string? actionType,
            string? tableAffected,
            string? dateFrom,
            string? dateTo)
        {
            try
            {
                var logs = await _context.AuditLogs
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                var userIds = logs
                    .Where(l => !string.IsNullOrEmpty(l.UserId))
                    .Select(l => l.UserId!)
                    .Distinct()
                    .ToList();

                List<UserEmailDto> users = new();
                if (userIds.Any())
                {
                    var idList = string.Join(",", userIds.Select(id => $"'{id}'"));
                    users = await _context.Database
                        .SqlQueryRaw<UserEmailDto>(
                            $"SELECT Id, Email FROM AspNetUsers WHERE Id IN ({idList})")
                        .ToListAsync();
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    var matchingUserIds = users
                        .Where(u => u.Email != null && u.Email.ToLower().Contains(s))
                        .Select(u => u.Id)
                        .ToHashSet();

                    logs = logs.Where(l =>
                        (l.UserId != null && matchingUserIds.Contains(l.UserId)) ||
                        (l.ActionType != null && l.ActionType.ToLower().Contains(s)) ||
                        (l.TableAffected != null && l.TableAffected.ToLower().Contains(s)) ||
                        (l.Details != null && l.Details.ToLower().Contains(s))
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(actionType))
                {
                    var a = actionType.Trim().ToLower();
                    logs = logs.Where(l =>
                        l.ActionType != null && l.ActionType.ToLower().Contains(a)
                    ).ToList();
                }

                if (!string.IsNullOrWhiteSpace(tableAffected))
                    logs = logs.Where(l => l.TableAffected == tableAffected).ToList();

                if (!string.IsNullOrWhiteSpace(dateFrom) &&
                    DateTime.TryParse(dateFrom, out var fromDate))
                    logs = logs.Where(l => l.Timestamp >= fromDate).ToList();

                if (!string.IsNullOrWhiteSpace(dateTo) &&
                    DateTime.TryParse(dateTo, out var toDate))
                    logs = logs.Where(l => l.Timestamp <= toDate.AddDays(1)).ToList();

                var userDict = users.ToDictionary(u => u.Id, u => u.Email);
                foreach (var log in logs)
                {
                    if (log.UserId != null && userDict.TryGetValue(log.UserId, out var email))
                        log.ApplicationUser = new ApplicationUser { Email = email };
                }

                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,User Email,Action Type,Table Affected,Details");

                foreach (var log in logs)
                {
                    sb.AppendLine(string.Join(",",
                        $"\"{log.Timestamp.ToLocalTime():MMM dd, yyyy HH:mm}\"",
                        $"\"{log.ApplicationUser?.Email ?? "Unknown"}\"",
                        $"\"{log.ActionType}\"",
                        $"\"{log.TableAffected}\"",
                        $"\"{log.Details?.Replace("\"", "\"\"")}\""));
                }

                var bytes = Encoding.UTF8.GetPreamble()
                    .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                    .ToArray();

                return File(bytes, "text/csv", $"auditlogs_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditLogs Export Error] {ex.Message}");
                TempData["Error"] = "Export failed. Please try again.";
                return RedirectToAction("AuditLogs");
            }
        }


        // ===============================
        // EXPENSES (reimbursed only)
        // ===============================
        [HttpGet]
        [Route("Expense")]
        public async Task<IActionResult> Expense(
            string search,
            string dateFrom,
            string dateTo)
        {
            var reimbursed = BudgetService.ReimbursedStatuses;

            // Base set: reimbursed expenses only
            var baseQuery = _context.Expenses
                .Include(e => e.User)
                .Where(e => reimbursed.Contains(e.Status));

            // Table query with filters
            var query = baseQuery;

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.ExpenseType.Contains(search) ||
                                         (e.Description != null && e.Description.Contains(search)) ||
                                         (e.PlateNumber != null && e.PlateNumber.Contains(search)) ||
                                         (e.User.FullName != null && e.User.FullName.Contains(search)));

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from.Date);

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var to))
            {
                var end = to.Date.AddDays(1);
                query = query.Where(e => e.ExpenseDate < end);
            }

            var expense = await query
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            // Stat cards (always across all reimbursed expenses)
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var totalReimbursed = await baseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            var reimbursedCount = await baseQuery.CountAsync();

            ViewBag.TotalReimbursed = totalReimbursed;
            ViewBag.ReimbursedCount = reimbursedCount;
            ViewBag.ReimbursedThisMonth = await baseQuery
                .Where(e => e.ExpenseDate >= monthStart)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;
            ViewBag.AverageClaim = reimbursedCount == 0 ? 0m : totalReimbursed / reimbursedCount;

            // Line chart: last 6 months of reimbursed money
            var start = monthStart.AddMonths(-5);

            var monthly = await baseQuery
                .Where(e => e.ExpenseDate >= start)
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => x.Amount)
                })
                .ToListAsync();

            var labels = new List<string>();
            var totals = new List<decimal>();

            for (int i = 0; i < 6; i++)
            {
                var m = start.AddMonths(i);
                var row = monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                labels.Add(m.ToString("MMM yyyy"));
                totals.Add(row?.Total ?? 0);
            }

            ViewBag.ChartLabels = labels;
            ViewBag.ChartTotals = totals;

            ViewBag.Search = search;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            ViewData["Title"] = "Expense";
            return View(expense);
        }

        // ===============================
        // DEPARTMENTS
        // ===============================
        [HttpGet]
        [Route("Department")]
        public async Task<IActionResult> Department()
        {
            var departments = await _context.Departments
                .Where(d => d.DepartmentName == "Finance" || d.DepartmentName == "Logistics")
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();

            // Member count per department, from the users' Department text
            var memberCounts = await _userManager.Users
                .Where(u => u.Department != null && !u.IsArchived)
                .GroupBy(u => u.Department!)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count);

            ViewBag.MemberCounts = memberCounts;
            ViewData["Title"] = "Departments";
            return View(departments);
        }

        [HttpPost]
        [Route("Department/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(int id, string? departmentHead, string status)
        {
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == id);
            if (dept == null)
            {
                TempData["Error"] = "Department not found.";
                return RedirectToAction("Department");
            }

            dept.DepartmentHead = string.IsNullOrWhiteSpace(departmentHead) ? null : departmentHead.Trim();
            dept.IsActive = status == "Active";

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Department '{dept.DepartmentName}' was updated.";
            return RedirectToAction("Department");
        }

        // ===============================
        // VEHICLES
        // ===============================
        [HttpGet]
        [Route("Vehicles")]
        public async Task<IActionResult> Vehicles()
        {
            var vehicles = await _context.Vehicles
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            // How many expenses each plate has (decides Delete vs. Retire)
            var usage = await _context.Expenses
                .Where(e => e.PlateNumber != null)
                .GroupBy(e => e.PlateNumber!)
                .Select(g => new { Plate = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Plate, x => x.Count);

            ViewBag.Usage = usage;
            ViewData["Title"] = "Vehicles";
            return View(vehicles);
        }

        [HttpPost]
        [Route("Vehicles/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVehicle(int vehicleId, string plateNumber, string? type, string status)
        {
            var plate = (plateNumber ?? "").Trim().ToUpperInvariant();

            if (!Regex.IsMatch(plate, "^[A-Z0-9]{6}$"))
            {
                TempData["Error"] = "Plate number must be exactly 6 letters/digits (e.g. LUX234).";
                return RedirectToAction("Vehicles");
            }

            if (!AllowedVehicleStatuses.Contains(status))
                status = "Active";

            var duplicate = await _context.Vehicles
                .AnyAsync(v => v.PlateNumber == plate && v.VehicleId != vehicleId);
            if (duplicate)
            {
                TempData["Error"] = $"Vehicle '{plate}' already exists.";
                return RedirectToAction("Vehicles");
            }

            Vehicle? vehicle;
            if (vehicleId > 0)
            {
                vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
                if (vehicle == null)
                {
                    TempData["Error"] = "Vehicle not found.";
                    return RedirectToAction("Vehicles");
                }

                // Keep existing expenses linked if the plate is renamed
                if (vehicle.PlateNumber != plate)
                {
                    var linked = await _context.Expenses
                        .Where(e => e.PlateNumber == vehicle.PlateNumber)
                        .ToListAsync();
                    foreach (var e in linked) e.PlateNumber = plate;
                }
            }
            else
            {
                vehicle = new Vehicle();
                _context.Vehicles.Add(vehicle);
            }

            vehicle.PlateNumber = plate;
            vehicle.Type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
            vehicle.Status = status;

            await _context.SaveChangesAsync();
            await WriteAuditAsync(vehicleId > 0 ? "Update Vehicle" : "Create Vehicle", "Vehicles",
                $"{plate} ({vehicle.Type ?? "no type"}) - {status}");

            TempData["Success"] = $"Vehicle '{plate}' saved.";
            return RedirectToAction("Vehicles");
        }

        [HttpPost]
        [Route("Vehicles/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle == null)
            {
                TempData["Error"] = "Vehicle not found.";
                return RedirectToAction("Vehicles");
            }

            var hasExpenses = await _context.Expenses
    .AnyAsync(e => e.PlateNumber == vehicle.PlateNumber || e.VehicleId == vehicle.VehicleId);
            if (hasExpenses)
            {
                TempData["Error"] = $"'{vehicle.PlateNumber}' has expense records and can't be deleted. Set it to Retired instead.";
                return RedirectToAction("Vehicles");
            }

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
            await WriteAuditAsync("Delete Vehicle", "Vehicles", vehicle.PlateNumber);

            TempData["Success"] = $"Vehicle '{vehicle.PlateNumber}' removed.";
            return RedirectToAction("Vehicles");
        }

        // ===============================
        // REPORTS
        // ===============================
        private async Task<List<ExpenseReportRow>> BuildReportRowsAsync(
            DateTime? from, DateTime? to, string? category, string? status, string? department)
        {
            var query = _context.Expenses.Include(e => e.User).AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.ExpenseDate >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(e => e.ExpenseDate < end);
            }

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.ExpenseType == category);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.Status == status);

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(e => e.User.Department == department);

            var list = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            return list.Select(e => new ExpenseReportRow
            {
                ExpenseId = e.Id,
                EmployeeName = e.User?.FullName ?? e.User?.Email ?? "Unknown",
                Department = string.IsNullOrWhiteSpace(e.User?.Department) ? "Unassigned" : e.User!.Department!,
                Category = string.IsNullOrWhiteSpace(e.ExpenseType) ? "Other" : e.ExpenseType,
                Description = e.Description ?? "",
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                Status = e.Status
            }).ToList();
        }

        [HttpGet]
        [Route("Reports")]
        public async Task<IActionResult> Reports(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? category,
            string? status,
            string? department,
            bool generated = false)
        {
            var today = DateTime.Today;
            var vm = new ExpenseReportViewModel
            {
                DateFrom = dateFrom ?? new DateTime(today.Year, today.Month, 1).AddMonths(-5),
                DateTo = dateTo ?? today,
                Category = category,
                Status = status,
                Department = department,
                Generated = generated
            };

            // Dropdown options
            vm.CategoryOptions = await _context.Expenses
                .Where(e => e.ExpenseType != null && e.ExpenseType != "")
                .Select(e => e.ExpenseType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            vm.DepartmentOptions = await _userManager.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            if (generated)
            {
                vm.Rows = await BuildReportRowsAsync(vm.DateFrom, vm.DateTo, category, status, department);

                var total = vm.GrandTotal;

                List<BreakdownItem> Breakdown(Func<ExpenseReportRow, string> key) =>
                    vm.Rows.GroupBy(key)
                        .Select(g =>
                        {
                            var amt = g.Sum(x => x.Amount);
                            return new BreakdownItem
                            {
                                Name = g.Key,
                                Count = g.Count(),
                                Total = amt,
                                Pct = total == 0 ? 0 : Math.Round((double)(amt / total * 100), 1)
                            };
                        })
                        .OrderByDescending(b => b.Total)
                        .ToList();

                vm.ByCategory = Breakdown(r => r.Category);
                vm.ByDepartment = Breakdown(r => r.Department);

                vm.TopClaimants = vm.Rows
                    .GroupBy(r => new { r.EmployeeName, r.Department })
                    .Select(g =>
                    {
                        var amt = g.Sum(x => x.Amount);
                        return new BreakdownItem
                        {
                            Name = g.Key.EmployeeName,
                            Sub = g.Key.Department,
                            Count = g.Count(),
                            Total = amt,
                            Pct = total == 0 ? 0 : Math.Round((double)(amt / total * 100), 1)
                        };
                    })
                    .OrderByDescending(b => b.Total)
                    .Take(10)
                    .ToList();

                // Monthly trend across the rows' date span (capped at 36 months)
                if (vm.Rows.Any())
                {
                    var first = vm.Rows.Min(r => r.ExpenseDate);
                    var last = vm.Rows.Max(r => r.ExpenseDate);
                    var cursor = new DateTime(first.Year, first.Month, 1);
                    var endMonth = new DateTime(last.Year, last.Month, 1);
                    var guard = 0;

                    while (cursor <= endMonth && guard++ < 36)
                    {
                        vm.TrendLabels.Add(cursor.ToString("MMM yyyy"));
                        vm.TrendData.Add(vm.Rows
                            .Where(r => r.ExpenseDate.Year == cursor.Year && r.ExpenseDate.Month == cursor.Month)
                            .Sum(r => r.Amount));
                        cursor = cursor.AddMonths(1);
                    }
                }
            }

            ViewData["Title"] = "Reports";
            return View(vm);
        }

        [HttpGet]
        [Route("Reports/Export")]
        public async Task<IActionResult> ExportReport(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? category,
            string? status,
            string? department)
        {
            var rows = await BuildReportRowsAsync(dateFrom, dateTo, category, status, department);

            // Quote values and neutralize spreadsheet formulas (=, +, -, @)
            static string Csv(string? value)
            {
                var v = value ?? "";
                if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Expense ID,Date,Employee,Department,Category,Description,Amount,Status");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv($"EXP-{r.ExpenseId:D4}"),
                    Csv(r.ExpenseDate.ToString("yyyy-MM-dd")),
                    Csv(r.EmployeeName),
                    Csv(r.Department),
                    Csv(r.Category),
                    Csv(r.Description),
                    r.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    Csv(r.Status)));
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv", $"expense_report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }
        // DTO for reading user email from AspNetUsers
        public class UserEmailDto
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
        }

        // ===============================
        // BUDGETS
        // ===============================
        [HttpGet]
        [Route("Budgets")]
        public async Task<IActionResult> Budgets(string? show)
        {
            var showAll = string.Equals(show, "all", StringComparison.OrdinalIgnoreCase);

            var vm = new BudgetPageViewModel
            {
                ShowAll = showAll,
                Rows = await _budgetService.GetRowsAsync(DateTime.Today, currentOnly: !showAll),
                Departments = await _context.Departments
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.DepartmentName)
                    .ToListAsync()
            };

            ViewData["Title"] = "Budget & Cost Control";
            return View(vm);
        }

        [HttpPost]
        [Route("Budgets/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBudget(BudgetInput model)
        {
            var dept = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == model.DepartmentId);

            if (dept == null)
            {
                TempData["Error"] = "Please choose a department.";
                return RedirectToAction("Budgets", new { show = "all" });
            }
            if (model.TotalAmount <= 0)
            {
                TempData["Error"] = "Budget amount must be greater than zero.";
                return RedirectToAction("Budgets", new { show = "all" });
            }
            if (model.EndDate.Date < model.StartDate.Date)
            {
                TempData["Error"] = "End date must be on or after the start date.";
                return RedirectToAction("Budgets", new { show = "all" });
            }

            var start = model.StartDate.Date;
            var end = model.EndDate.Date;

            // No overlapping active budgets for the same department
            var overlap = await _context.Budgets.AnyAsync(b =>
                b.IsActive &&
                b.DepartmentId == model.DepartmentId &&
                b.BudgetId != model.BudgetId &&
                b.StartDate <= end && b.EndDate >= start);

            if (overlap)
            {
                TempData["Error"] = $"{dept.DepartmentName} already has a budget that overlaps these dates.";
                return RedirectToAction("Budgets", new { show = "all" });
            }

            var threshold = (model.AlertThresholdPercent < 1 || model.AlertThresholdPercent > 100)
                ? 80 : model.AlertThresholdPercent;

            var committed = await _budgetService.GetCommittedAsync(dept.DepartmentName, start, end);

            Budget budget;
            if (model.BudgetId > 0)
            {
                budget = await _context.Budgets.FirstOrDefaultAsync(b => b.BudgetId == model.BudgetId);
                if (budget == null)
                {
                    TempData["Error"] = "Budget not found.";
                    return RedirectToAction("Budgets", new { show = "all" });
                }
            }
            else
            {
                budget = new Budget { IsActive = true };
                _context.Budgets.Add(budget);
            }

            budget.DepartmentId = model.DepartmentId;
            budget.StartDate = start;
            budget.EndDate = end;
            budget.TotalAmount = model.TotalAmount;
            budget.RemainingAmount = model.TotalAmount - committed;
            budget.AlertThresholdPercent = threshold;
            budget.BlockWhenExceeded = model.BlockWhenExceeded;

            await _context.SaveChangesAsync();
            await WriteBudgetAuditAsync(model.BudgetId > 0 ? "Update Budget" : "Create Budget",
                $"{dept.DepartmentName} {start:yyyy-MM-dd} to {end:yyyy-MM-dd} = {model.TotalAmount:N2}");

            TempData["Success"] = "Budget saved.";
            return RedirectToAction("Budgets", new { show = "all" });
        }

        [HttpPost]
        [Route("Budgets/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var b = await _context.Budgets.Include(x => x.Department)
                .FirstOrDefaultAsync(x => x.BudgetId == id);
            if (b == null)
            {
                TempData["Error"] = "Budget not found.";
                return RedirectToAction("Budgets", new { show = "all" });
            }

            _context.Budgets.Remove(b);
            await _context.SaveChangesAsync();
            await WriteBudgetAuditAsync("Delete Budget",
                $"{b.Department?.DepartmentName} {b.StartDate:yyyy-MM-dd} to {b.EndDate:yyyy-MM-dd}");

            TempData["Success"] = "Budget removed.";
            return RedirectToAction("Budgets", new { show = "all" });
        }

        // Creates the next period with the same amount and settings
        [HttpPost]
        [Route("Budgets/Renew")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewBudget(int id)
        {
            var b = await _context.Budgets.Include(x => x.Department)
                .FirstOrDefaultAsync(x => x.BudgetId == id);
            if (b == null)
            {
                TempData["Error"] = "Budget not found.";
                return RedirectToAction("Budgets", new { show = "all" });
            }

            var s = b.StartDate.Date;
            var e = b.EndDate.Date;
            var newStart = e.AddDays(1);
            DateTime newEnd;

            // Whole calendar months -> renew by the same number of months
            var wholeMonths = s.Day == 1 && e == new DateTime(e.Year, e.Month, 1).AddMonths(1).AddDays(-1);
            if (wholeMonths)
            {
                var months = (e.Year - s.Year) * 12 + e.Month - s.Month + 1;
                newEnd = newStart.AddMonths(months).AddDays(-1);
            }
            else
            {
                var days = (e - s).Days + 1;
                newEnd = newStart.AddDays(days - 1);
            }

            var overlap = await _context.Budgets.AnyAsync(x =>
                x.IsActive && x.DepartmentId == b.DepartmentId &&
                x.StartDate <= newEnd && x.EndDate >= newStart);
            if (overlap)
            {
                TempData["Error"] = "The next period already has a budget for this department.";
                return RedirectToAction("Budgets", new { show = "all" });
            }

            _context.Budgets.Add(new Budget
            {
                DepartmentId = b.DepartmentId,
                StartDate = newStart,
                EndDate = newEnd,
                TotalAmount = b.TotalAmount,
                RemainingAmount = b.TotalAmount,
                AlertThresholdPercent = b.AlertThresholdPercent,
                BlockWhenExceeded = b.BlockWhenExceeded,
                IsActive = true
            });

            await _context.SaveChangesAsync();
            await WriteBudgetAuditAsync("Renew Budget",
                $"{b.Department?.DepartmentName} {newStart:yyyy-MM-dd} to {newEnd:yyyy-MM-dd}");

            TempData["Success"] = $"Renewed for {newStart:MMM d, yyyy} to {newEnd:MMM d, yyyy}.";
            return RedirectToAction("Budgets", new { show = "all" });
        }

        // ===============================
        // AUDIT HELPERS
        // ===============================
        private Task WriteBudgetAuditAsync(string action, string details)
            => WriteAuditAsync(action, "Budgets", details);

        private async Task WriteAuditAsync(string action, string table, string details)
        {
            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = _userManager.GetUserId(User),
                    ActionType = action,
                    TableAffected = table,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            catch { /* auditing must never break the request */ }
        }
    }
}