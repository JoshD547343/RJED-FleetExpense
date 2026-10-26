
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Approval_Logs")]
    public class ApprovalLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApprovalLogId { get; set; }

        public int ExpenseId { get; set; }
        public int ReviewerId { get; set; }

        [Required, MaxLength(20)]
        public string Action { get; set; } = string.Empty; // Approved | Rejected | Validated

        [MaxLength(50)]
        public string? Comments { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.UtcNow;

        // Navigation
        public Expense Expense { get; set; } = null!;
        public User Reviewer { get; set; } = null!;
    }
}