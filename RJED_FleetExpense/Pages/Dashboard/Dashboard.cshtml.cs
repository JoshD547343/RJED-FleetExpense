using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using RJED_FleetExpense.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RJED_FleetExpense.Pages.Dashboard
{
    [Authorize(Roles = "Employee")]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public EmployeeDashboardViewModel Model { get; set; } = new();

        public DashboardModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                RedirectToPage("/Account/Login");
                return;
            }

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var myExpenses = await _context.Expenses
                .Where(e => e.UserId == user.Id)
                .ToListAsync();

            Model = new EmployeeDashboardViewModel
            {
                CurrentUserName = user.FullName ?? user.UserName ?? "Employee",
                CurrentUserEmail = user.Email ?? "—",
                EmployeeId = $"EMP-{user.Id.Substring(0, 6).ToUpper()}",
                Today = DateTime.Today.ToString("dddd, MMMM d, yyyy"),

                // Fixed: Use a safe date for sorting
                MyExpenses = myExpenses
                    .OrderByDescending(e => e.DateSubmitted != default ? e.DateSubmitted : e.ExpenseDate)
                    .ToList(),

                MyPendingExpenses = myExpenses.Count(e => e.Status == "Pending"),

                MyTotalThisMonth = myExpenses.Count(e => e.ExpenseDate.Month == currentMonth &&
                                                         e.ExpenseDate.Year == currentYear),

                MyTotalThisMonthAmount = myExpenses
                    .Where(e => e.ExpenseDate.Month == currentMonth &&
                                e.ExpenseDate.Year == currentYear)
                    .Sum(e => e.Amount),

                RecentlyApproved = myExpenses.Count(e => e.Status == "Approved"),

                RejectedThisMonth = myExpenses.Count(e => e.Status == "Rejected" &&
                                                          e.ExpenseDate.Month == currentMonth &&
                                                          e.ExpenseDate.Year == currentYear)
            };
        }
    }
}