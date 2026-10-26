using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RJED_FleetExpense.Data;

namespace RJED_FleetExpense.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("audit_log_id")]
        public int AuditLogId { get; set; }

        [Column("user_id")]
        [MaxLength(450)]
        public string? UserId { get; set; }

        [Column("action_type")]
        [MaxLength(30)]
        public string? ActionType { get; set; }

        [Column("table_affected")]
        [MaxLength(50)]
        public string? TableAffected { get; set; }

        [Column("details")]
        [MaxLength(500)]
        public string? Details { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public ApplicationUser? ApplicationUser { get; set; }
    }
}