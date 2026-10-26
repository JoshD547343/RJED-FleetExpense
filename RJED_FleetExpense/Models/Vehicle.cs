using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Vehicles")]
    public class Vehicle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VehicleId { get; set; }

        [Required, MaxLength(20)]
        public string PlateNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Type { get; set; } // e.g. "Truck", "Van"

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active | Maintenance | Retired

        // The AssignedDriverId column still exists in the database (nullable)
        // but is intentionally not mapped. EF ignores it.

        // Navigation (required by ApplicationDbContext: .WithMany(v => v.Expenses))
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}