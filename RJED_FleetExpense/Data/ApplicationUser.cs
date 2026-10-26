using Microsoft.AspNetCore.Identity;
using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; } = false;
        public int LockoutStage { get; set; } = 0;  // tracks progressive lockout stage

        // Navigation
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}