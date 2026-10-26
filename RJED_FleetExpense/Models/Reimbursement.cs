using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RJED_FleetExpense.Data;

namespace RJED_FleetExpense.Models
{
    [Table("Reimbursements")]
    public class Reimbursement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReimbursementId { get; set; }

        public int ExpenseId { get; set; }
        public string ProcessedById { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? TransactionRef { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        public DateTime PaymentDate { get; set; }

        // Navigation
        public Expense Expense { get; set; } = null!;
        public ApplicationUser ProcessedBy { get; set; } = null!;
    }
}