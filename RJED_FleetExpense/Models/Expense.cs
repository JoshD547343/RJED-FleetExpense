using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RJED_FleetExpense.Data;

namespace RJED_FleetExpense.Models
{
    [Table("Expenses")]
    public class Expense
    {
        // ===============================
        // PRIMARY KEY
        // ===============================
        [Key]
        public int Id { get; set; }

        // ===============================
        // USER (ASP.NET Identity)
        // ===============================
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // ===============================
        // CORE FIELDS
        // ===============================
        [Required]
        [MaxLength(50)]
        public string ExpenseType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public DateTime ExpenseDate { get; set; }

        [MaxLength(20)]
        public string? PlateNumber { get; set; }

        [MaxLength(100)]
        public string? Description { get; set; }

        // ===============================
        // FINAL SYSTEM STATUS
        // ===============================
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        // ===============================
        // SUPERVISOR APPROVAL FLOW
        // ===============================
        [MaxLength(20)]
        public string? SupervisorStatus { get; set; }

        public DateTime? SupervisorApprovedDate { get; set; }

        public DateTime? SupervisorRejectedDate { get; set; }

        [MaxLength(500)]
        public string? SupervisorRemarks { get; set; }

        [MaxLength(100)]
        public string? RejectionReason { get; set; }

        // ===============================
        // DEPARTMENT HEAD APPROVAL FLOW
        // ===============================
        [MaxLength(20)]
        public string? DepartmentStatus { get; set; }

        public DateTime? DepartmentApprovedDate { get; set; }

        public DateTime? DepartmentRejectedDate { get; set; }

        [MaxLength(500)]
        public string? DepartmentRemarks { get; set; }

        // ===============================
        // FORWARDING / TRACKING
        // ===============================
        public bool? IsForwarded { get; set; }

        [MaxLength(100)]
        public string? ForwardedTo { get; set; }

        // ===============================
        // OPTIONAL FIELDS
        // ===============================
        [MaxLength(255)]
        public string? ReceiptImageUrl { get; set; }

        public int? CategoryId { get; set; }
        public ExpenseCategory? Category { get; set; }

        public int? VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // ===============================
        // RELATIONS
        // ===============================
        public ICollection<ExpenseReceipt> Receipts { get; set; } = new List<ExpenseReceipt>();

        public Reimbursement? Reimbursement { get; set; }




        public TripDetail? TripDetail { get; set; }

        public ICollection<ApprovalLog> ApprovalLogs { get; set; } = new List<ApprovalLog>();
    }
}