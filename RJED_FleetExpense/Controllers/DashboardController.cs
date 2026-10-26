using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using RJED_FleetExpense.Services;

namespace RJED_FleetExpense.Controllers
{
    [Authorize(Roles = "Super_Admin")]
    public class DashboardController : Controller
    {
        private const string ViewFolder = "~/Views/SuperAdmin/";
        private const int AuditPageSize = 20;

        private static readonly string[] AssignableRoles =
        {
            "Employee", "Supervisor", "Department_head", "Accounting", "Admin", "Super_Admin"
        };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ISettingsService _settings;
        private readonly IBackupService _backup;

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ISettingsService settings,
            IBackupService backup)
        {
            _userManager = userManager;
            _db = db;
            _settings = settings;
            _backup = backup;
        }

        // /Dashboard
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Employee"))
                return Redirect("/Employee/Dashboard");

            // Placeholder dashboard data (replace with real queries later)
            ViewBag.TotalUsers = 42;
            ViewBag.TotalExpensesThisMonth = 124500.75m;
            ViewBag.PendingApprovals = 6;
            ViewBag.ActiveDepartments = 4;
            ViewBag.CurrentUserEmail = User.Identity?.Name ?? "Guest";

            return View(ViewFolder + "Dashboard.cshtml");
        }

        // /Dashboard/Dashboard  (the URL Login redirects Super_Admin to)
        [HttpGet]
        public Task<IActionResult> Dashboard() => Index();

        // ───────────── MANAGE USERS ─────────────

        [HttpGet]
        public async Task<IActionResult> ManageUser()
        {
            ViewData["Title"] = "Manage Users & Roles";
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            return View(ViewFolder + "ManageUser.cshtml", users);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            string fullName, string email, string role, string department,
            string password, string confirmPassword)
        {
            if (!AssignableRoles.Contains(role))
                return Fail("Please select a valid role.");

            if (password != confirmPassword)
                return Fail("Passwords do not match.");

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Department = department,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                return Fail(string.Join(" ", roleResult.Errors.Select(e => e.Description)));

            return Success($"User '{fullName}' created as {role}.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(
            string userId, string fullName, string role, string department, string status)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Fail("User not found.");

            var isSelf = user.Id == _userManager.GetUserId(User);
            var currentRoles = await _userManager.GetRolesAsync(user);
            var wasSuperAdmin = currentRoles.Contains("Super_Admin");

            // Role/status are omitted for the signed-in user (locked in the UI) -> keep existing values
            var newRole = string.IsNullOrWhiteSpace(role) ? currentRoles.FirstOrDefault() : role;
            var newActive = string.IsNullOrWhiteSpace(status) ? user.IsActive : status == "Active";

            if (newRole == null || !AssignableRoles.Contains(newRole))
                return Fail("Invalid role.");

            var losingSuperAdminAccess = !newActive || newRole != "Super_Admin";

            if (isSelf && losingSuperAdminAccess)
                return Fail("You can't deactivate or demote your own account.");

            if (wasSuperAdmin && user.IsActive && losingSuperAdminAccess
                && await CountActiveSuperAdminsAsync() <= 1)
                return Fail("There must be at least one active Super Admin.");

            user.FullName = fullName;
            user.Department = department;
            user.IsActive = newActive;

            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
                return Fail(string.Join(" ", update.Errors.Select(e => e.Description)));

            if (!currentRoles.Contains(newRole))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, newRole);
            }

            return Success($"'{user.FullName}' updated.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Fail("User not found.");

            if (user.Id == _userManager.GetUserId(User))
                return Fail("You can't archive your own account.");

            if (user.IsActive)
                return Fail("Set the user to Inactive before archiving.");

            user.IsArchived = true;
            await _userManager.UpdateAsync(user);
            return Success($"'{user.FullName ?? user.Email}' archived.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Fail("User not found.");

            user.IsArchived = false;
            user.IsActive = false; // restored as Inactive; set Active from Edit
            await _userManager.UpdateAsync(user);
            return Success($"'{user.FullName ?? user.Email}' restored as Inactive.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Fail("User not found.");

            if (user.Id == _userManager.GetUserId(User))
                return Fail("You can't delete your own account.");

            if (!user.IsArchived)
                return Fail("Only archived users can be deleted.");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            return Success($"'{user.FullName ?? user.Email}' deleted permanently.");
        }

        // ───────────── EXPENSE REPORTS ─────────────

        [HttpGet]
        public async Task<IActionResult> Expenses(
            string report = "all", string? search = null, string? status = null,
            string? dateFrom = null, string? dateTo = null, string? department = null,
            string? export = null)
        {
            var validReports = new[] { "all", "department", "employee", "type", "vehicle", "aging" };
            if (!validReports.Contains(report)) report = "all";

            var query = _db.Expenses.Include(e => e.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(e =>
                    (e.User.FullName ?? "").ToLower().Contains(s) ||
                    (e.User.Email ?? "").ToLower().Contains(s) ||
                    (e.PlateNumber ?? "").ToLower().Contains(s) ||
                    (e.ExpenseType ?? "").ToLower().Contains(s) ||
                    (e.Description ?? "").ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.Status == status);
            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(e => e.User.Department == department);
            if (DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.ExpenseDate >= from.Date);
            if (DateTime.TryParse(dateTo, out var to))
                query = query.Where(e => e.ExpenseDate < to.Date.AddDays(1));

            var list = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            // Echo filters back to the view
            ViewBag.Report = report;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Department = department;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Departments = await _userManager.Users
                .Where(u => u.Department != null && u.Department != "")
                .Select(u => u.Department!).Distinct().OrderBy(d => d).ToListAsync();

            // Stat cards
            decimal SumBy(string st) => list.Where(e => e.Status == st).Sum(e => e.Amount);
            ViewBag.PendingCount = list.Count(e => e.Status == "Pending");
            ViewBag.ApprovedCount = list.Count(e => e.Status == "Approved");
            ViewBag.RejectedCount = list.Count(e => e.Status == "Rejected");
            ViewBag.TotalSubmitted = list.Count;
            ViewBag.TotalPending = SumBy("Pending");
            ViewBag.TotalApproved = SumBy("Approved");
            ViewBag.TotalRejected = SumBy("Rejected");

            // 6-month trend
            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var months = Enumerable.Range(0, 6).Select(i => thisMonth.AddMonths(i - 5)).ToList();
            bool InMonth(Expense e, DateTime m) => e.ExpenseDate.Year == m.Year && e.ExpenseDate.Month == m.Month;
            ViewBag.ChartLabels = months.Select(m => m.ToString("MMM yyyy")).ToList();
            ViewBag.ChartTotals = months.Select(m => list.Where(e => InMonth(e, m)).Sum(e => e.Amount)).ToList();
            ViewBag.ChartApproved = months.Select(m => list.Where(e => InMonth(e, m) && e.Status == "Approved").Sum(e => e.Amount)).ToList();

            // Grouped reports
            List<ExpenseGroupRow>? rows = null;
            switch (report)
            {
                case "department":
                    ViewBag.GroupLabel = "Department";
                    rows = list.GroupBy(e => string.IsNullOrWhiteSpace(e.User?.Department) ? "No Department" : e.User!.Department!)
                               .Select(g => ToRow(g.Key, null, g)).OrderByDescending(r => r.Total).ToList();
                    break;
                case "employee":
                    ViewBag.GroupLabel = "Employee";
                    rows = list.GroupBy(e => e.User?.FullName ?? e.User?.Email ?? "Unknown")
                               .Select(g => ToRow(g.Key, g.First().User?.Department, g)).OrderByDescending(r => r.Total).ToList();
                    break;
                case "type":
                    ViewBag.GroupLabel = "Expense Type";
                    rows = list.GroupBy(e => string.IsNullOrWhiteSpace(e.ExpenseType) ? "Unspecified" : e.ExpenseType)
                               .Select(g => ToRow(g.Key, null, g)).OrderByDescending(r => r.Total).ToList();
                    break;
                case "vehicle":
                    ViewBag.GroupLabel = "Vehicle (Plate)";
                    rows = list.GroupBy(e => string.IsNullOrWhiteSpace(e.PlateNumber) ? "No plate" : e.PlateNumber.Trim().ToUpper())
                               .Select(g => ToRow(g.Key, null, g)).OrderByDescending(r => r.Total).ToList();
                    break;
            }
            ViewBag.ReportRows = rows;

            // Pending aging
            var pending = list.Where(e => e.Status == "Pending").OrderBy(e => e.ExpenseDate).ToList();
            if (report == "aging")
            {
                var defs = new[] { ("0–3 days", 0, 3), ("4–7 days", 4, 7), ("8–14 days", 8, 14), ("15+ days", 15, int.MaxValue) };
                ViewBag.AgingBuckets = defs.Select(d =>
                {
                    var items = pending.Where(e =>
                    {
                        var age = Math.Max(0, (DateTime.Today - e.ExpenseDate.Date).Days);
                        return age >= d.Item2 && age <= d.Item3;
                    });
                    return ToRow(d.Item1, null, items);
                }).ToList();
            }

            // CSV export of whatever the current report shows
            if (export == "csv")
            {
                var sb = new StringBuilder();
                if (rows != null)
                {
                    sb.AppendLine($"{Csv((string)ViewBag.GroupLabel)},Count,Total,Approved,Pending,Rejected,Approval Rate %");
                    foreach (var r in rows)
                        sb.AppendLine($"{Csv(r.Label)},{r.Count},{r.Total:0.00},{r.Approved:0.00},{r.Pending:0.00},{r.Rejected:0.00},{r.ApprovalRate:0.0}");
                }
                else
                {
                    sb.AppendLine("ID,Employee,Department,Date,Type,Plate,Description,Amount,Status");
                    foreach (var e in report == "aging" ? pending : list)
                        sb.AppendLine(string.Join(",",
                            $"EXP-{e.Id:D4}", Csv(e.User?.FullName ?? e.User?.Email), Csv(e.User?.Department),
                            e.ExpenseDate.ToString("yyyy-MM-dd"), Csv(e.ExpenseType), Csv(e.PlateNumber),
                            Csv(e.Description), e.Amount.ToString("0.00"), Csv(e.Status)));
                }
                return File(Encoding.UTF8.GetBytes("\uFEFF" + sb.ToString()), "text/csv",
                            $"expenses-{report}-{DateTime.Now:yyyyMMdd}.csv");
            }

            ViewData["Title"] = "Expense Reports";
            return View(ViewFolder + "Expenses.cshtml", report == "aging" ? pending : list);
        }

        // ───────────── AUDIT LOGS ─────────────

        // Login-related events (login, logout, failed login)
        private static readonly Expression<Func<AuditLog, bool>> IsAuthEvent = l =>
            (l.ActionType ?? "").ToLower().Contains("login") ||
            (l.ActionType ?? "").ToLower().Contains("logout") ||
            (l.ActionType ?? "").ToLower().Contains("fail");

        private static readonly Expression<Func<AuditLog, bool>> IsFailedLogin = l =>
            (l.ActionType ?? "").ToLower().Contains("fail");

        private static readonly Expression<Func<AuditLog, bool>> IsSuccessLogin = l =>
            (l.ActionType ?? "").ToLower().Contains("login") &&
            !(l.ActionType ?? "").ToLower().Contains("fail");

        private IQueryable<AuditLog> BuildAuditQuery(
            string view, string? search, string? actionType, string? tableAffected,
            string? dateFrom, string? dateTo)
        {
            var q = _db.AuditLogs.Include(l => l.ApplicationUser).AsQueryable();

            if (view != "all")
                q = q.Where(IsAuthEvent);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(l =>
                    (l.ApplicationUser!.Email ?? "").ToLower().Contains(s) ||
                    (l.ApplicationUser!.FullName ?? "").ToLower().Contains(s) ||
                    (l.ActionType ?? "").ToLower().Contains(s) ||
                    (l.Details ?? "").ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                if (actionType == "failed")
                    q = q.Where(IsFailedLogin);
                else
                {
                    var a = actionType.ToLower();
                    q = q.Where(l => (l.ActionType ?? "").ToLower().Contains(a));
                }
            }

            if (!string.IsNullOrWhiteSpace(tableAffected))
                q = q.Where(l => l.TableAffected == tableAffected);

            // Timestamps are stored in UTC; the filter dates are local dates
            if (DateTime.TryParse(dateFrom, out var from))
            {
                var fromUtc = from.Date.ToUniversalTime();
                q = q.Where(l => l.Timestamp >= fromUtc);
            }
            if (DateTime.TryParse(dateTo, out var to))
            {
                var toUtc = to.Date.AddDays(1).ToUniversalTime();
                q = q.Where(l => l.Timestamp < toUtc);
            }

            return q;
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogs(
            string view = "logins", string? search = null, string? actionType = null,
            string? tableAffected = null, string? dateFrom = null, string? dateTo = null,
            int page = 1)
        {
            if (view != "all" && view != "users") view = "logins";

            ViewBag.View = view;
            ViewBag.Search = search;
            ViewBag.ActionType = actionType;
            ViewBag.TableAffected = tableAffected;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            ViewBag.Tables = await _db.AuditLogs
                .Where(l => l.TableAffected != null && l.TableAffected != "")
                .Select(l => l.TableAffected!).Distinct().OrderBy(t => t).ToListAsync();

            // Stat cards (not affected by filters)
            var todayUtc = DateTime.Today.ToUniversalTime();
            var since24h = DateTime.UtcNow.AddHours(-24);
            ViewBag.LoginsToday = await _db.AuditLogs.Where(IsSuccessLogin)
                .CountAsync(l => l.Timestamp >= todayUtc);
            ViewBag.UsersToday = await _db.AuditLogs.Where(IsSuccessLogin)
                .Where(l => l.Timestamp >= todayUtc && l.UserId != null)
                .Select(l => l.UserId).Distinct().CountAsync();
            ViewBag.FailedRecent = await _db.AuditLogs.Where(IsFailedLogin)
                .CountAsync(l => l.Timestamp >= since24h);

            ViewData["Title"] = "Audit Logs";

            // ── User Login Status: one row per user ──
            if (view == "users")
            {
                var events = await BuildAuditQuery("logins", null, null, null, dateFrom, dateTo)
                    .Where(l => l.UserId != null)
                    .Select(l => new { l.UserId, l.ActionType, l.Timestamp })
                    .ToListAsync();

                var byUser = events
                    .GroupBy(e => e.UserId!)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var users = await _userManager.Users.Where(u => !u.IsArchived).ToListAsync();
                var rows = new List<UserLoginRow>();

                foreach (var u in users)
                {
                    var ev = byUser.TryGetValue(u.Id, out var found) ? found : null;
                    var successes = ev?.Where(x => (x.ActionType ?? "").ToLower().Contains("login")
                                                && !(x.ActionType ?? "").ToLower().Contains("fail")).ToList();
                    var failed = ev?.Count(x => (x.ActionType ?? "").ToLower().Contains("fail")) ?? 0;
                    var roles = await _userManager.GetRolesAsync(u);

                    rows.Add(new UserLoginRow
                    {
                        Email = u.Email ?? "",
                        FullName = u.FullName ?? "",
                        Department = u.Department ?? "",
                        Role = roles.FirstOrDefault() ?? "—",
                        IsActive = u.IsActive,
                        LastLogin = successes != null && successes.Any() ? successes.Max(x => x.Timestamp) : (DateTime?)null,
                        LoginCount = successes?.Count ?? 0,
                        FailedCount = failed
                    });
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    rows = rows.Where(r => r.Email.ToLower().Contains(s)
                                        || r.FullName.ToLower().Contains(s)
                                        || r.Department.ToLower().Contains(s)
                                        || r.Role.ToLower().Contains(s)).ToList();
                }

                rows = rows.OrderByDescending(r => r.LastLogin ?? DateTime.MinValue)
                           .ThenBy(r => r.FullName).ToList();

                ViewBag.UserRows = rows;
                ViewBag.NeverLoggedIn = rows.Count(r => r.LastLogin == null);
                ViewBag.Total = rows.Count;
                ViewBag.Page = 1;
                ViewBag.TotalPages = 1;
                return View(ViewFolder + "AuditLogs.cshtml", Enumerable.Empty<AuditLog>());
            }

            // ── Login Activity / All Activity: paged event list ──
            var query = BuildAuditQuery(view, search, actionType, tableAffected, dateFrom, dateTo);
            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AuditPageSize));
            page = Math.Clamp(page, 1, totalPages);

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * AuditPageSize)
                .Take(AuditPageSize)
                .ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            return View(ViewFolder + "AuditLogs.cshtml", logs);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditLogs(
            string view = "logins", string? search = null, string? actionType = null,
            string? tableAffected = null, string? dateFrom = null, string? dateTo = null)
        {
            var logs = await BuildAuditQuery(view == "all" ? "all" : "logins",
                                             search, actionType, tableAffected, dateFrom, dateTo)
                .OrderByDescending(l => l.Timestamp).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,User,Full Name,Action,Table,Details");
            foreach (var l in logs)
                sb.AppendLine(string.Join(",",
                    Csv(l.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    Csv(l.ApplicationUser?.Email),
                    Csv(l.ApplicationUser?.FullName),
                    Csv(l.ActionType),
                    Csv(l.TableAffected),
                    Csv(l.Details)));

            return File(Encoding.UTF8.GetBytes("\uFEFF" + sb.ToString()), "text/csv",
                        $"audit-logs-{view}-{DateTime.Now:yyyyMMdd}.csv");
        }

        // ───────────── OTHER PAGES ─────────────

        [HttpGet]
        public IActionResult WorkflowRules()
        {
            ViewData["Title"] = "Workflow Rules";
            return View(ViewFolder + "WorkflowRules" + ".cshtml");
        }

        // ───────────── SYSTEM SETTINGS ─────────────

        [HttpGet]
        public async Task<IActionResult> SystemSetting()
        {
            ViewData["Title"] = "System Settings";
            var s = await _settings.GetAsync();

            var vm = new SystemSettingsViewModel
            {
                Input = s,
                Backups = _backup.List(s.BackupFolder),
                ActiveTab = TempData["ActiveTab"] as string ?? "general"
            };
            try { vm.Db = await _backup.GetInfoAsync(); } catch { /* DB info is optional */ }

            return View(ViewFolder + "SystemSetting.cshtml", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGeneral([Bind(Prefix = "Input")] Models.SystemSetting input)
        {
            if (string.IsNullOrWhiteSpace(input.CompanyName)) return SettingsFail("general", "Company name is required.");
            if (!IsEmail(input.SystemEmail)) return SettingsFail("general", "Enter a valid system email address.");
            if (input.Currency is not ("PHP" or "USD")) return SettingsFail("general", "Choose a supported currency.");

            await _settings.SaveAsync(s =>
            {
                s.CompanyName = input.CompanyName.Trim();
                s.Currency = input.Currency;
                s.SystemEmail = input.SystemEmail.Trim();
            });
            return SettingsOk("general", "General settings saved.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExpense([Bind(Prefix = "Input")] Models.SystemSetting input)
        {
            if (input.AutoApproveBelow < 0 || input.ReceiptRequiredAbove < 0)
                return SettingsFail("expense", "Amounts can't be negative.");
            if (input.ClaimFilingDays is < 1 or > 365)
                return SettingsFail("expense", "Filing window must be between 1 and 365 days.");

            await _settings.SaveAsync(s =>
            {
                s.AutoApproveBelow = input.AutoApproveBelow;
                s.ReceiptRequiredAbove = input.ReceiptRequiredAbove;
                s.ClaimFilingDays = input.ClaimFilingDays;
            });
            return SettingsOk("expense", "Expense policy saved.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSecurity([Bind(Prefix = "Input")] Models.SystemSetting input)
        {
            if (input.SessionTimeoutMinutes is < 5 or > 480) return SettingsFail("security", "Session timeout must be 5 to 480 minutes.");
            if (input.MaxLoginAttempts is < 3 or > 10) return SettingsFail("security", "Login attempts must be 3 to 10.");
            if (input.MinPasswordLength is < 6 or > 32) return SettingsFail("security", "Minimum password length must be 6 to 32.");

            await _settings.SaveAsync(s =>
            {
                s.SessionTimeoutMinutes = input.SessionTimeoutMinutes;
                s.MaxLoginAttempts = input.MaxLoginAttempts;
                s.MinPasswordLength = input.MinPasswordLength;
                s.RequireComplexPassword = input.RequireComplexPassword;
                s.MaintenanceMode = input.MaintenanceMode;
                s.MaintenanceMessage = string.IsNullOrWhiteSpace(input.MaintenanceMessage)
                    ? "The system is under maintenance. Please try again later."
                    : input.MaintenanceMessage.Trim();
            });
            return SettingsOk("security", input.MaintenanceMode
                ? "Security settings saved. Maintenance mode is ON: only admins can use the system."
                : "Security settings saved.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotifications([Bind(Prefix = "Input")] Models.SystemSetting input)
        {
            await _settings.SaveAsync(s =>
            {
                s.EmailOnApproval = input.EmailOnApproval;
                s.SmsOnEscalation = input.SmsOnEscalation;
            });
            return SettingsOk("notifications", "Notification preferences saved.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBackup([Bind(Prefix = "Input")] Models.SystemSetting input)
        {
            if (input.BackupRetentionDays is < 1 or > 365) return SettingsFail("backup", "Keep backups for 1 to 365 days.");
            if (string.IsNullOrWhiteSpace(input.BackupFolder) || !Path.IsPathRooted(input.BackupFolder))
                return SettingsFail("backup", "Enter a full folder path, for example C:\\RJED_Backups.");
            try { Directory.CreateDirectory(input.BackupFolder); }
            catch (Exception ex) { return SettingsFail("backup", "Can't use that folder: " + ex.Message); }

            await _settings.SaveAsync(s =>
            {
                s.AutoBackupEnabled = input.AutoBackupEnabled;
                s.AutoBackupTime = input.AutoBackupTime;
                s.BackupRetentionDays = input.BackupRetentionDays;
                s.BackupFolder = input.BackupFolder.Trim();
            });
            return SettingsOk("backup", "Backup schedule saved.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupNow()
        {
            var s = await _settings.GetAsync();
            try
            {
                var f = await _backup.CreateBackupAsync(s.BackupFolder);
                return SettingsOk("backup", $"Backup created: {f.Name} ({SystemSettingsViewModel.FormatSize(f.SizeBytes)}).");
            }
            catch (Exception ex)
            {
                return SettingsFail("backup", "Backup failed: " + ex.Message +
                    " (SQL Server needs write access to the backup folder.)");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadBackup(string file)
        {
            var s = await _settings.GetAsync();
            try
            {
                var path = _backup.GetFullPath(s.BackupFolder, file);
                return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
            }
            catch (Exception ex) { return SettingsFail("backup", ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBackup(string file)
        {
            var s = await _settings.GetAsync();
            try { _backup.Delete(s.BackupFolder, file); return SettingsOk("backup", "Backup deleted."); }
            catch (Exception ex) { return SettingsFail("backup", ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBackup(string file, string confirmText)
        {
            if (!string.Equals(confirmText?.Trim(), "RESTORE", StringComparison.Ordinal))
                return SettingsFail("backup", "Type RESTORE exactly to confirm.");

            var s = await _settings.GetAsync();
            try
            {
                // Safety net: snapshot the current data first.
                var safety = await _backup.CreateBackupAsync(s.BackupFolder);
                await _backup.RestoreAsync(s.BackupFolder, file);
                return SettingsOk("backup", $"Database restored from {file}. Pre-restore copy saved as {safety.Name}.");
            }
            catch (Exception ex) { return SettingsFail("backup", "Restore failed: " + ex.Message); }
        }

        private IActionResult SettingsOk(string tab, string msg)
        {
            TempData["Success"] = msg;
            TempData["ActiveTab"] = tab;
            return RedirectToAction(nameof(SystemSetting));
        }

        private IActionResult SettingsFail(string tab, string msg)
        {
            TempData["Error"] = msg;
            TempData["ActiveTab"] = tab;
            return RedirectToAction(nameof(SystemSetting));
        }

        private static bool IsEmail(string? v)
        {
            try { return !string.IsNullOrWhiteSpace(v) && new MailAddress(v).Address == v.Trim(); }
            catch { return false; }
        }

        // ───────────── HELPERS ─────────────

        private static ExpenseGroupRow ToRow(string label, string? sub, IEnumerable<Expense> items)
        {
            var l = items.ToList();
            return new ExpenseGroupRow
            {
                Label = label,
                Sub = sub,
                Count = l.Count,
                ApprovedCount = l.Count(x => x.Status == "Approved"),
                Total = l.Sum(x => x.Amount),
                Approved = l.Where(x => x.Status == "Approved").Sum(x => x.Amount),
                Pending = l.Where(x => x.Status == "Pending").Sum(x => x.Amount),
                Rejected = l.Where(x => x.Status == "Rejected").Sum(x => x.Amount)
            };
        }

        private static string Csv(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";

        private async Task<int> CountActiveSuperAdminsAsync()
        {
            var superAdmins = await _userManager.GetUsersInRoleAsync("Super_Admin");
            return superAdmins.Count(u => u.IsActive && !u.IsArchived);
        }

        private IActionResult Success(string message)
        {
            TempData["Success"] = message;
            return RedirectToAction(nameof(ManageUser));
        }

        private IActionResult Fail(string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(ManageUser));
        }
    }
}