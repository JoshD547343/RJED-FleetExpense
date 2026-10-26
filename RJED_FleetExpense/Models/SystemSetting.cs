namespace RJED_FleetExpense.Models
{
    /// <summary>Single-row table (Id = 1) that stores every global setting.</summary>
    public class SystemSetting
    {
        public int Id { get; set; }

        // General
        public string CompanyName { get; set; } = "RJED Fleet Logistics";
        public string Currency { get; set; } = "PHP";
        public string SystemEmail { get; set; } = "no-reply@rjedfleet.ph";

        // Expense policy
        public decimal AutoApproveBelow { get; set; } = 0;          // 0 = never auto-approve
        public decimal ReceiptRequiredAbove { get; set; } = 500;
        public int ClaimFilingDays { get; set; } = 30;               // days after the expense a claim can be filed

        // Security
        public int SessionTimeoutMinutes { get; set; } = 30;
        public int MaxLoginAttempts { get; set; } = 5;
        public int MinPasswordLength { get; set; } = 8;
        public bool RequireComplexPassword { get; set; } = true;
        public bool MaintenanceMode { get; set; }
        public string MaintenanceMessage { get; set; } = "The system is under maintenance. Please try again later.";

        // Notifications
        public bool EmailOnApproval { get; set; } = true;
        public bool SmsOnEscalation { get; set; }

        // Backup
        public bool AutoBackupEnabled { get; set; }
        public TimeSpan AutoBackupTime { get; set; } = new TimeSpan(2, 0, 0);
        public int BackupRetentionDays { get; set; } = 14;
        public string BackupFolder { get; set; } = @"C:\RJED_Backups";
        public DateTime? LastAutoBackupAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}