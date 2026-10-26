using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Budgets")]
    public class Budget
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BudgetId { get; set; }

        public int DepartmentId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Snapshot refreshed when a budget is saved. The page calculates live values.
        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAmount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Cost control
        public int AlertThresholdPercent { get; set; } = 80;
        public bool BlockWhenExceeded { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Navigation
        public Department Department { get; set; } = null!;
    }
}