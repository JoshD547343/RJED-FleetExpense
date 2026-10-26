using System;

namespace RJED_FleetExpense.Models
{
    public class UserLoginRow
    {
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Role { get; set; } = "—";
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public int LoginCount { get; set; }
        public int FailedCount { get; set; }
    }
}