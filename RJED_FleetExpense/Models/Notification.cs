using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RJED_FleetExpense.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        public int UserId { get; set; }

        [MaxLength(50)]
        public string? Title { get; set; }

        [MaxLength(100)]
        public string? Message { get; set; }

        [Required, MaxLength(10)]
        public string ReadStatus { get; set; } = "Unread"; // Read | Unread

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
    }
}