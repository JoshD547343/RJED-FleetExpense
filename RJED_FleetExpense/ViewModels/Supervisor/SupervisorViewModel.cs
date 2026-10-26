using RJED_FleetExpense.Data;
using RJED_FleetExpense.Models;
using System;
using System.Collections.Generic;

namespace RJED.ViewModels.Supervisor
{
    // ── Shared expense row (Approved + Rejected list views) ──
    public class ExpenseRowViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = "";
        public string Department { get; set; } = "";
        public string ExpenseType { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? RejectedDate { get; set; }
        public string? SupervisorRemarks { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsForwarded { get; set; }
        public string? ForwardedTo { get; set; }
    }

    // ── Pending Expenses page ──
    public class PendingExpensesPageViewModel
    {
        public List<Expense> Expenses { get; set; } = new();
        public int PendingCount { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    // ── Approved Expenses page ──
    public class ApprovedExpensesPageViewModel
    {
        public List<ExpenseRowViewModel> Expenses { get; set; } = new();
        public decimal TotalApprovedAmount { get; set; }
        public int TotalCount { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages =>
            (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // ── Rejected Expenses page ──
    public class RejectedExpensesPageViewModel
    {
        public List<ExpenseRowViewModel> Expenses { get; set; } = new();
        public int TotalCount { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? ReasonFilter { get; set; }
        public List<string> RejectionReasons { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages =>
            (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // ── My Team page ──
    public class TeamMemberViewModel
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public string AvatarInitials { get; set; } = "?";
        public int TotalSubmitted { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public decimal TotalAmountSubmitted { get; set; }
        public DateTime? LastSubmittedDate { get; set; }
    }

    public class MyTeamPageViewModel
    {
        public List<TeamMemberViewModel> TeamMembers { get; set; } = new();
        public int TotalTeamMembers { get; set; }
        public int TotalPendingAcrossTeam { get; set; }
        public List<string> Departments { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? DepartmentFilter { get; set; }
        public string? MostActiveEmployeeName { get; set; }
        public int MostActiveEmployeeCount { get; set; }
    }
}