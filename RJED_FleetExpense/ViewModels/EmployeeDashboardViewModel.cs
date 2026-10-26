using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.ViewModels
{
    public class EmployeeDashboardViewModel
    {
        public string CurrentUserName { get; set; } = "";
        public string CurrentUserEmail { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string Today { get; set; } = "";

        // Stats
        public int MyPendingExpenses { get; set; } = 0;
        public int MyTotalThisMonth { get; set; } = 0;
        public decimal MyTotalThisMonthAmount { get; set; } = 0;
        public int RecentlyApproved { get; set; } = 0;
        public int RejectedThisMonth { get; set; } = 0;

        // Lists
        public List<Expense> MyExpenses { get; set; } = new();
        public List<Expense> MyPendingExpensesList { get; set; } = new();
        public List<Expense> RecentlyApprovedList { get; set; } = new();

        // For Today / Recent
        public List<Expense> TodayExpenses { get; set; } = new();

        // Vehicles (dropdown on the Submit Expense page)
        public List<Vehicle> Vehicles { get; set; } = new();

        // plate -> dates ("yyyy-MM-dd") already used by a non-rejected expense
        public Dictionary<string, List<string>> BookedDates { get; set; } = new();
    }
}