using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RJED_FleetExpense.Services
{
    /// <summary>Checks every minute; runs one backup per day at the configured time, then prunes old files.</summary>
    public class AutoBackupService : BackgroundService
    {
        private readonly ISettingsService _settings;
        private readonly IBackupService _backup;
        private readonly ILogger<AutoBackupService> _log;

        public AutoBackupService(ISettingsService settings, IBackupService backup, ILogger<AutoBackupService> log)
        {
            _settings = settings;
            _backup = backup;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var s = await _settings.GetAsync();
                    var now = DateTime.Now;
                    var dueToday = s.AutoBackupEnabled
                                   && now.TimeOfDay >= s.AutoBackupTime
                                   && (s.LastAutoBackupAt == null || s.LastAutoBackupAt.Value.Date < now.Date);

                    if (dueToday)
                    {
                        var file = await _backup.CreateBackupAsync(s.BackupFolder);
                        var removed = _backup.DeleteOlderThan(s.BackupFolder, s.BackupRetentionDays);
                        await _settings.SaveAsync(x => x.LastAutoBackupAt = DateTime.Now);
                        _log.LogInformation("Auto backup {File} created, {Removed} old backup(s) removed.", file.Name, removed);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Auto backup failed.");
                    // Don't retry every minute on failure: mark today as attempted.
                    try { await _settings.SaveAsync(x => x.LastAutoBackupAt = DateTime.Now); } catch { }
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}