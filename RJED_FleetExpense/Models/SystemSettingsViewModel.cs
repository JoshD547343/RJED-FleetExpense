using RJED_FleetExpense.Services;

namespace RJED_FleetExpense.Models
{
    public class SystemSettingsViewModel
    {
        public SystemSetting Input { get; set; } = new();
        public DbInfo? Db { get; set; }
        public List<BackupFileInfo> Backups { get; set; } = new();
        public string ActiveTab { get; set; } = "general";

        public static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double n = bytes; int i = 0;
            while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
            return $"{n:0.#} {u[i]}";
        }
    }
}