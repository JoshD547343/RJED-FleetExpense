using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Departments")]
    public class Department
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DepartmentId { get; set; }

        [Required, MaxLength(50)]
        public string DepartmentName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CostCenterCode { get; set; }

        [MaxLength(150)]
        public string? DepartmentHead { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}