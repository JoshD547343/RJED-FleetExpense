using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using System.Security.Claims;

namespace RJED_FleetExpense.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        // For authenticated actions (existing — unchanged)
        public async Task LogAsync(string actionType, string? tableAffected = null, string? details = null)
        {
            try
            {
                var httpUser = _httpContextAccessor.HttpContext?.User;
                if (httpUser?.Identity?.IsAuthenticated != true)
                    return;

                var userId = httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return;

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    ActionType = actionType,
                    TableAffected = tableAffected,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditLog Error] {ex.Message}");
            }
        }

        // For login events — user may not be authenticated yet
        public async Task LogLoginAsync(string? userId, string actionType, string? details = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    ActionType = actionType,
                    TableAffected = "AspNetUsers",
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditLog Error] {ex.Message}");
            }
        }
    }
}