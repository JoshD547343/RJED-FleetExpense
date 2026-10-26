using Microsoft.Data.SqlClient;

namespace RJED_FleetExpense.Services
{
    public record BackupFileInfo(string Name, long SizeBytes, DateTime CreatedAt);
    public record DbInfo(string Database, string Server, double SizeMb, string Version);

    public interface IBackupService
    {
        Task<BackupFileInfo> CreateBackupAsync(string folder);
        List<BackupFileInfo> List(string folder);
        string GetFullPath(string folder, string fileName);
        void Delete(string folder, string fileName);
        Task RestoreAsync(string folder, string fileName);
        int DeleteOlderThan(string folder, int days);
        Task<DbInfo> GetInfoAsync();
    }

    /// <summary>
    /// SQL Server backups using BACKUP DATABASE.
    /// NOTE: the .bak file is written by the SQL Server service, so the folder must be
    /// on the SQL Server machine and the SQL Server service account needs write access to it.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly string _connStr;
        private readonly string _dbName;

        public BackupService(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            var b = new SqlConnectionStringBuilder(_connStr);
            _dbName = string.IsNullOrEmpty(b.InitialCatalog) ? "master" : b.InitialCatalog;
        }

        private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

        public async Task<BackupFileInfo> CreateBackupAsync(string folder)
        {
            Directory.CreateDirectory(folder);
            var fileName = $"{_dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var fullPath = Path.Combine(folder, fileName);

            var sql = $"BACKUP DATABASE {Quote(_dbName)} TO DISK = @path WITH INIT, CHECKSUM, NAME = @name";
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 900 };
            cmd.Parameters.AddWithValue("@path", fullPath);
            cmd.Parameters.AddWithValue("@name", $"{_dbName} full backup");
            await cmd.ExecuteNonQueryAsync();

            var fi = new FileInfo(fullPath);
            return new BackupFileInfo(fi.Name, fi.Exists ? fi.Length : 0, fi.Exists ? fi.CreationTime : DateTime.Now);
        }

        public List<BackupFileInfo> List(string folder)
        {
            if (!Directory.Exists(folder)) return new();
            return new DirectoryInfo(folder).GetFiles("*.bak")
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new BackupFileInfo(f.Name, f.Length, f.CreationTime))
                .ToList();
        }

        /// <summary>Strips any path parts so a request can never escape the backup folder.</summary>
        public string GetFullPath(string folder, string fileName)
        {
            var safe = Path.GetFileName(fileName ?? "");
            if (string.IsNullOrWhiteSpace(safe) || !safe.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid backup file name.");
            var full = Path.Combine(folder, safe);
            if (!File.Exists(full)) throw new FileNotFoundException("Backup file not found.");
            return full;
        }

        public void Delete(string folder, string fileName) => File.Delete(GetFullPath(folder, fileName));

        public async Task RestoreAsync(string folder, string fileName)
        {
            var fullPath = GetFullPath(folder, fileName);

            // Must connect to master: the target database is taken offline for the restore.
            var b = new SqlConnectionStringBuilder(_connStr) { InitialCatalog = "master" };
            var db = Quote(_dbName);
            var sql = $@"
                ALTER DATABASE {db} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                BEGIN TRY
                    RESTORE DATABASE {db} FROM DISK = @path WITH REPLACE;
                END TRY
                BEGIN CATCH
                    ALTER DATABASE {db} SET MULTI_USER;
                    THROW;
                END CATCH;
                ALTER DATABASE {db} SET MULTI_USER;";

            await using var conn = new SqlConnection(b.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 1800 };
            cmd.Parameters.AddWithValue("@path", fullPath);
            await cmd.ExecuteNonQueryAsync();
            SqlConnection.ClearAllPools();
        }

        public int DeleteOlderThan(string folder, int days)
        {
            if (!Directory.Exists(folder)) return 0;
            var cutoff = DateTime.Now.AddDays(-days);
            var old = new DirectoryInfo(folder).GetFiles($"{_dbName}_*.bak").Where(f => f.CreationTime < cutoff).ToList();
            old.ForEach(f => f.Delete());
            return old.Count;
        }

        public async Task<DbInfo> GetInfoAsync()
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                @"SELECT @@SERVERNAME,
                         CAST(SUM(size) * 8 / 1024.0 AS float),
                         LEFT(@@VERSION, CHARINDEX(CHAR(10), @@VERSION) - 1)
                  FROM sys.database_files", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return new DbInfo(_dbName, r.GetString(0), Math.Round(r.GetDouble(1), 1), r.GetString(2).Trim());
            return new DbInfo(_dbName, "", 0, "");
        }
    }
}