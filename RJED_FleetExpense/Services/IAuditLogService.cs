namespace RJED_FleetExpense.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string actionType, string? tableAffected = null, string? details = null);
        Task LogLoginAsync(string? userId, string actionType, string? details = null);
    }
}