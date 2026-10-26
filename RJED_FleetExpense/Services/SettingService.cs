using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.Services
{
    public interface ISettingsService
    {
        Task<SystemSetting> GetAsync();
        Task SaveAsync(Action<SystemSetting> apply);
    }

    /// <summary>
    /// Singleton. Uses plain SQL (no EF model change, no migration).
    /// Creates the dbo.SystemSettings table the first time it runs if it does not exist yet.
    /// Settings are cached for 30 seconds and the cache is cleared on every save.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private const string CacheKey = "system-settings";
        private static readonly PropertyInfo[] Props = typeof(SystemSetting)
            .GetProperties().Where(p => p.Name != nameof(SystemSetting.Id)).ToArray();

        private readonly string _connStr;
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _tableReady;

        public SettingsService(IConfiguration config, IMemoryCache cache)
        {
            _connStr = config.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _cache = cache;
        }

        public async Task<SystemSetting> GetAsync()
        {
            if (_cache.TryGetValue(CacheKey, out SystemSetting? cached) && cached != null) return cached;
            var s = await LoadAsync();
            _cache.Set(CacheKey, s, TimeSpan.FromSeconds(30));
            return s;
        }

        public async Task SaveAsync(Action<SystemSetting> apply)
        {
            var s = await LoadAsync();
            apply(s);
            s.UpdatedAt = DateTime.UtcNow;

            var sets = string.Join(", ", Props.Select(p => $"[{p.Name}] = @{p.Name}"));
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($"UPDATE dbo.SystemSettings SET {sets} WHERE Id = 1", conn);
            foreach (var p in Props)
                cmd.Parameters.AddWithValue("@" + p.Name, p.GetValue(s) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            _cache.Remove(CacheKey);
        }

        private async Task<SystemSetting> LoadAsync()
        {
            await EnsureTableAsync();
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT * FROM dbo.SystemSettings WHERE Id = 1", conn);
            await using var r = await cmd.ExecuteReaderAsync();

            var s = new SystemSetting { Id = 1 };
            if (await r.ReadAsync())
            {
                foreach (var p in Props)
                {
                    var v = r[p.Name];
                    p.SetValue(s, v is DBNull ? null : v);
                }
            }
            return s;
        }

        private async Task EnsureTableAsync()
        {
            if (_tableReady) return;
            await _lock.WaitAsync();
            try
            {
                if (_tableReady) return;
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(CreateTableSql, conn);
                await cmd.ExecuteNonQueryAsync();
                _tableReady = true;
            }
            finally { _lock.Release(); }
        }

        // Same script as CreateSystemSettings.sql, so you can also run it by hand.
        private const string CreateTableSql = @"
IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings (
        Id                     int            NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
        CompanyName            nvarchar(150)  NOT NULL CONSTRAINT DF_SS_CompanyName DEFAULT N'RJED Fleet Logistics',
        Currency               nvarchar(3)    NOT NULL CONSTRAINT DF_SS_Currency DEFAULT N'PHP',
        SystemEmail            nvarchar(256)  NOT NULL CONSTRAINT DF_SS_SystemEmail DEFAULT N'no-reply@rjedfleet.ph',
        AutoApproveBelow       decimal(10,2)  NOT NULL CONSTRAINT DF_SS_AutoApprove DEFAULT 0,
        ReceiptRequiredAbove   decimal(10,2)  NOT NULL CONSTRAINT DF_SS_Receipt DEFAULT 500,
        ClaimFilingDays        int            NOT NULL CONSTRAINT DF_SS_FilingDays DEFAULT 30,
        SessionTimeoutMinutes  int            NOT NULL CONSTRAINT DF_SS_Timeout DEFAULT 30,
        MaxLoginAttempts       int            NOT NULL CONSTRAINT DF_SS_Attempts DEFAULT 5,
        MinPasswordLength      int            NOT NULL CONSTRAINT DF_SS_MinPw DEFAULT 8,
        RequireComplexPassword bit            NOT NULL CONSTRAINT DF_SS_Complex DEFAULT 1,
        MaintenanceMode        bit            NOT NULL CONSTRAINT DF_SS_Maint DEFAULT 0,
        MaintenanceMessage     nvarchar(300)  NOT NULL CONSTRAINT DF_SS_MaintMsg DEFAULT N'The system is under maintenance. Please try again later.',
        EmailOnApproval        bit            NOT NULL CONSTRAINT DF_SS_Email DEFAULT 1,
        SmsOnEscalation        bit            NOT NULL CONSTRAINT DF_SS_Sms DEFAULT 0,
        AutoBackupEnabled      bit            NOT NULL CONSTRAINT DF_SS_AutoBk DEFAULT 0,
        AutoBackupTime         time(0)        NOT NULL CONSTRAINT DF_SS_BkTime DEFAULT '02:00:00',
        BackupRetentionDays    int            NOT NULL CONSTRAINT DF_SS_Retention DEFAULT 14,
        BackupFolder           nvarchar(500)  NOT NULL CONSTRAINT DF_SS_BkFolder DEFAULT N'C:\RJED_Backups',
        LastAutoBackupAt       datetime2      NULL,
        UpdatedAt              datetime2      NOT NULL CONSTRAINT DF_SS_Updated DEFAULT SYSUTCDATETIME()
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE Id = 1)
    INSERT INTO dbo.SystemSettings (Id) VALUES (1);";
    }
}