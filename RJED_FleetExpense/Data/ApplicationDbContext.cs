using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RJED_FleetExpense.Models;

namespace RJED_FleetExpense.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Vehicle> Vehicles { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<Budget> Budgets { get; set; } = null!;
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<ExpenseReceipt> ExpenseReceipts { get; set; } = null!;
        public DbSet<TripDetail> TripDetails { get; set; } = null!;
        public DbSet<ApprovalLog> ApprovalLogs { get; set; } = null!;
        public DbSet<Reimbursement> Reimbursements { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");

            // ================== EXPENSE ==================
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.User)
                .WithMany(u => u.Expenses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================== USER ==================
            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== BUDGET ==================
            modelBuilder.Entity<Budget>()
                .HasOne(b => b.Department)
                .WithMany(d => d.Budgets)
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== PROJECT ==================
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Projects)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== EXPENSE CATEGORY ==================
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== EXPENSE VEHICLE ==================
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Vehicle)
                .WithMany(v => v.Expenses)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== EXPENSE PROJECT ==================
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Project)
                .WithMany(p => p.Expenses)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== REIMBURSEMENT ==================
            modelBuilder.Entity<Reimbursement>()
                .HasOne(r => r.Expense)
                .WithOne(e => e.Reimbursement)
                .HasForeignKey<Reimbursement>(r => r.ExpenseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reimbursement>()
                .HasOne(r => r.ProcessedBy)
                .WithMany()
                .HasForeignKey(r => r.ProcessedById)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== TRIP DETAIL ==================
            modelBuilder.Entity<TripDetail>()
                .HasOne(t => t.Expense)
                .WithOne(e => e.TripDetail)
                .HasForeignKey<TripDetail>(t => t.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================== APPROVAL LOG ==================
            modelBuilder.Entity<ApprovalLog>()
                .HasOne(a => a.Expense)
                .WithMany(e => e.ApprovalLogs)
                .HasForeignKey(a => a.ExpenseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApprovalLog>()
                .HasOne(a => a.Reviewer)
                .WithMany()
                .HasForeignKey(a => a.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================== AUDIT LOG ==================
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.AuditLogId);

                entity.Property(e => e.AuditLogId)
                      .HasColumnName("audit_log_id")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.UserId)
                      .HasColumnName("user_id")
                      .HasMaxLength(450)
                      .IsRequired(false);            // nullable — unknown email attempts

                entity.Property(e => e.ActionType)
                      .HasColumnName("action_type")
                      .HasMaxLength(30);

                entity.Property(e => e.TableAffected)
                      .HasColumnName("table_affected")
                      .HasMaxLength(50);

                entity.Property(e => e.Details)
                      .HasColumnName("details")
                      .HasMaxLength(500);

                entity.Property(e => e.Timestamp)
                      .HasColumnName("timestamp");

                // Optional FK — SetNull so null UserId rows don't break Include()
                entity.HasOne(al => al.ApplicationUser)
                      .WithMany()
                      .HasForeignKey(al => al.UserId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ================== NOTIFICATION ==================
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================== DECIMAL PRECISION ==================
            modelBuilder.Entity<Budget>().Property(b => b.TotalAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Budget>().Property(b => b.RemainingAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Expense>().Property(e => e.Amount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Reimbursement>().Property(r => r.AmountPaid).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<TripDetail>().Property(t => t.DistanceKm).HasColumnType("decimal(7,2)");
            modelBuilder.Entity<TripDetail>().Property(t => t.FuelLiters).HasColumnType("decimal(7,2)");
        }
    }
}