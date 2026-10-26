using RJED_FleetExpense.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Expense_Categories")]
    public class ExpenseCategory
    {
        [Key]
        [Column("category_id")]  // ← maps to actual DB column name
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Required, MaxLength(50)]
        public string CategoryName { get; set; } = string.Empty;
        public bool RequiresReceipt { get; set; } = false;

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}