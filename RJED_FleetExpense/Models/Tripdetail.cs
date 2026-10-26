using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Trip_Details")]
    public class TripDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TripId { get; set; }

        public int ExpenseId { get; set; }

        [MaxLength(100)]
        public string? StartLocation { get; set; }

        [MaxLength(100)]
        public string? EndLocation { get; set; }

        [Column(TypeName = "decimal(4,2)")]
        public decimal? DistanceKm { get; set; }

        [Column(TypeName = "decimal(4,2)")]
        public decimal? FuelLiters { get; set; }

        // Navigation
        public Expense Expense { get; set; } = null!;
    }
}