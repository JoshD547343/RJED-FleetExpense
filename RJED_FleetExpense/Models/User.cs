using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required, MaxLength(25)]
        public string LastName { get; set; } = string.Empty;

        [Required, MaxLength(25)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(25)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(25)]
        public string Role { get; set; } = string.Empty;

        public int DepartmentId { get; set; }

        [Required, MaxLength(10)]
        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Department Department { get; set; } = null!;

        // Notifications only - REMOVE AuditLogs collection
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}