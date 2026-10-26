using System.ComponentModel.DataAnnotations;

namespace RJED_FleetExpense.Models  // ← adjust namespace if your Models folder uses a different one
{
    public class EmployeeProfileViewModel
    {
        // Editable fields (these will be bound by asp-for and posted back)
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        // Optional: if you want to let user enter/edit Employee ID / Badge Number
        [Display(Name = "Employee ID / Badge Number")]
        [StringLength(50)]
        public string? EmployeeId { get; set; }

        // ────────────────────────────────────────────────
        // Read-only / display-only fields (shown in UI, not meant for editing)
        // These help avoid extra ViewBag/ViewData usage
        [Display(Name = "Display Name")]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        // You can add more display-only properties if needed, e.g.
        // public string JobTitle { get; set; }
        // public DateTime? DateJoined { get; set; }
    }
}